using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.WebSocket.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateWebSocketStubs(
        CSharpCompilation compilation,
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken)
    {
        var wsAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketAttribute");
        var sendAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketSendAttribute");
        var receiveAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketReceiveAttribute");
        var connectAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketConnectAttribute");
        var closeAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketCloseAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        return IoProxyModelAssembly.Parse<WebSocketMemberModel, WebSocketInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
            cancellationToken,
            coreReferenced: wsAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.WebSocketCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<WebSocketInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) => TryAddMethod(
                method,
                marked.InterfaceSymbol,
                compilation,
                sendAttribute,
                receiveAttribute,
                connectAttribute,
                closeAttribute,
                observableType,
                unitType,
                members,
                diagnostics),
            tryAddProperty: (marked, property, members, diagnostics) => TryAddProperty(
                property,
                marked.InterfaceSymbol,
                compilation,
                sendAttribute,
                receiveAttribute,
                connectAttribute,
                closeAttribute,
                observableType,
                members,
                diagnostics),
            createInterface: static (marked, className, members) => new WebSocketInterfaceModel(
                $"{marked.InterfaceSymbol.GetSafeHintName()}.WebSocket.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.WebSocket"),
                members,
                marked.Nullability),
            createContext: static interfaces => new ContextGenerationModel(interfaces),
            tryAddOther: (marked, member, _, diagnostics) =>
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidWebSocketMember,
                        member.Locations.FirstOrDefault(),
                        marked.InterfaceSymbol.Name,
                        member.Name)));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? receiveAttribute,
        INamedTypeSymbol? connectAttribute,
        INamedTypeSymbol? closeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<WebSocketMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (receiveAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, receiveAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        WebSocketBoundaryKind? boundary = null;

        if (sendAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, sendAttribute))
        {
            boundary = WebSocketBoundaryKind.Send;
        }
        else if (connectAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, connectAttribute))
        {
            boundary = WebSocketBoundaryKind.Connect;
        }
        else if (closeAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, closeAttribute))
        {
            boundary = WebSocketBoundaryKind.Close;
        }

        if (boundary is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidWebSocketMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter",
                observableType,
                unitType,
                requiresUnitPayload: boundary.Value is WebSocketBoundaryKind.Send
                    or WebSocketBoundaryKind.Connect
                    or WebSocketBoundaryKind.Close,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                method.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return;
        }

        // Connect: must have exactly one Uri parameter
        if (boundary == WebSocketBoundaryKind.Connect)
        {
            var nonCtParams = method.Parameters
                .Where(static p => !IdentifierHelper.IsCancellationToken(p.Type))
                .ToList();
            if (nonCtParams.Count != 1 || nonCtParams[0].Type.ToDisplayString() != "System.Uri")
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedWebSocketOption,
                        method.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        method.Name));
                return;
            }
        }

        // Close: no non-CT parameters allowed
        if (boundary == WebSocketBoundaryKind.Close)
        {
            var nonCtParams = method.Parameters
                .Where(static p => !IdentifierHelper.IsCancellationToken(p.Type))
                .ToList();
            if (nonCtParams.Count != 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedWebSocketOption,
                        method.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        method.Name));
                return;
            }
        }

        if (IdentifierHelper.HasNonTrailingCancellationToken(method))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidWebSocketMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        var (declarations, names, ctName) = BuildParameters(method);
        members.Add(
            new WebSocketMemberModel(
                IdentifierHelper.Escape(method.Name),
                boundary.Value,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                names.ToImmutableEquatableArray(),
                ctName,
                ClassifySendPayload(boundary.Value, method)));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? receiveAttribute,
        INamedTypeSymbol? connectAttribute,
        INamedTypeSymbol? closeAttribute,
        INamedTypeSymbol? observableType,
        List<WebSocketMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (HasMethodBoundaryOnProperty(property, sendAttribute, connectAttribute, closeAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (receiveAttribute is null || !IoProxyInterfaceWalk.HasAttribute(property, receiveAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidWebSocketMember,
                    property.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    property.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter",
                observableType,
                unitType: null,
                requiresUnitPayload: false,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                property.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return;
        }

        members.Add(
            new WebSocketMemberModel(
                IdentifierHelper.Escape(property.Name),
                WebSocketBoundaryKind.Receive,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                null,
                WebSocketSendPayloadKind.None));
    }

    static bool HasMethodBoundaryOnProperty(
        IPropertySymbol property,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? connectAttribute,
        INamedTypeSymbol? closeAttribute) =>
        (sendAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, sendAttribute))
        || (connectAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, connectAttribute))
        || (closeAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, closeAttribute));
    static (List<string> declarations, List<string> names, string? cancellationTokenParameterName) BuildParameters(
        IMethodSymbol method)
    {
        var declarations = new List<string>();
        var names = new List<string>();
        string? ctName = null;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            if (i == method.Parameters.Length - 1 && IdentifierHelper.IsCancellationToken(parameter.Type))
            {
                ctName = IdentifierHelper.Escape(parameter.Name);
                declarations.Add(
                    $"{parameter.Type.ToDisplayString(DisplayFormat)} {ctName} = default");
                continue;
            }

            names.Add(IdentifierHelper.Escape(parameter.Name));
            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)}");
        }

        return (declarations, names, ctName);
    }

    static WebSocketSendPayloadKind ClassifySendPayload(WebSocketBoundaryKind boundary, IMethodSymbol method)
    {
        if (boundary != WebSocketBoundaryKind.Send)
        {
            return WebSocketSendPayloadKind.None;
        }

        ITypeSymbol? payloadType = null;
        var payloadCount = 0;
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            if (i == method.Parameters.Length - 1 && IdentifierHelper.IsCancellationToken(parameter.Type))
            {
                continue;
            }

            payloadCount++;
            payloadType = parameter.Type;
        }

        if (payloadCount == 0)
        {
            return WebSocketSendPayloadKind.None;
        }

        if (payloadCount > 1)
        {
            return WebSocketSendPayloadKind.Json;
        }

        if (payloadType!.SpecialType == SpecialType.System_String)
        {
            return WebSocketSendPayloadKind.Text;
        }

        if (payloadType is IArrayTypeSymbol array
            && array.ElementType.SpecialType == SpecialType.System_Byte)
        {
            return WebSocketSendPayloadKind.Binary;
        }

        return WebSocketSendPayloadKind.Json;
    }
}
