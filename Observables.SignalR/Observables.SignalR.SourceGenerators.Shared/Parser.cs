using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.SignalR.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateHubStubs(
        CSharpCompilation compilation,
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var hubAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubAttribute");
        if (hubAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.SignalRCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>()));
        }

        var invokeAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubInvokeAttribute");
        var sendAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubSendAttribute");
        var streamAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubStreamAttribute");
        var onAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubOnAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        var interfaces = new List<HubInterfaceModel>();

        foreach (var marked in markedInterfaces)
        {
            if (string.Equals(compilation.AssemblyName, "GeneratorTests", StringComparison.Ordinal)
                && marked.Syntax.Identifier.ValueText == "IInternalErrorProbe")
            {
                throw new InvalidOperationException("fail-safe probe");
            }

            var ifaceSymbol = marked.InterfaceSymbol;
            var nullable = marked.Nullability;

            var members = new List<HubMemberModel>();
            foreach (var member in marked.PublicInstanceMembers)
            {

                switch (member)
                {
                    case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                        TryAddMethod(
                            method,
                            ifaceSymbol,
                            compilation,
                            invokeAttribute,
                            sendAttribute,
                            streamAttribute,
                            onAttribute,
                            observableType,
                            unitType,
                            members,
                            diagnostics);
                        break;
                    case IPropertySymbol property:
                        TryAddProperty(
                            property,
                            compilation,
                            invokeAttribute,
                            sendAttribute,
                            streamAttribute,
                            onAttribute,
                            observableType,
                            members,
                            diagnostics);
                        break;
                }
            }

            if (members.Count == 0)
            {
                continue;
            }

            var className = $"{ifaceSymbol.Name.TrimStart('I')}GeneratedProxy";
            interfaces.Add(
                new HubInterfaceModel(
                    $"{className}.SignalR.g.cs",
                    className,
                    ifaceSymbol.ToDisplayString(DisplayFormat),
                    BackendTokens.QualifyGeneratedNamespace("Observables.SignalR"),
                    members.ToImmutableEquatableArray(),
                    nullable));
        }

        return (diagnostics, new ContextGenerationModel(interfaces.ToImmutableEquatableArray()));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute,
        INamedTypeSymbol? onAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<HubMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (onAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, onAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        var boundary = GetBoundaryKind(method, invokeAttribute, sendAttribute, streamAttribute);
        if (boundary is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryGetLiteralMethodName(method, boundary.Value, out var hubMethodName, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (IsUnsupportedStreamingParameter(parameter.Type))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedStreamingParameter,
                        parameter.Locations.FirstOrDefault(),
                        parameter.Name,
                        method.Name));
                return;
            }
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.SignalR.Reactive.SystemReactiveSignalRAdapter",
                observableType,
                unitType,
                requiresUnitPayload: boundary.Value == HubBoundaryKind.Send,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                method.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return;
        }

        var (declarations, names, hasCt) = BuildParameters(method);
        members.Add(
            new HubMemberModel(
                IdentifierHelper.Escape(method.Name),
                hubMethodName,
                boundary.Value,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                names.ToImmutableEquatableArray(),
                hasCt));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute,
        INamedTypeSymbol? onAttribute,
        INamedTypeSymbol? observableType,
        List<HubMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (HasMethodBoundaryOnProperty(property, invokeAttribute, sendAttribute, streamAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (onAttribute is null || !IoProxyInterfaceWalk.HasAttribute(property, onAttribute))
        {
            if (property.GetAttributes().Length > 0 || property.Name is not "Hub")
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHubMember,
                        property.Locations.FirstOrDefault(),
                        property.ContainingType.Name,
                        property.Name));
            }

            return;
        }

        if (!TryGetLiteralMethodNameFromProperty(property, out var hubMethodName, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.SignalR.Reactive.SystemReactiveSignalRAdapter",
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
            new HubMemberModel(
                IdentifierHelper.Escape(property.Name),
                hubMethodName,
                HubBoundaryKind.On,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                false));
    }

    static HubBoundaryKind? GetBoundaryKind(
        IMethodSymbol method,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute)
    {
        if (invokeAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, invokeAttribute))
        {
            return HubBoundaryKind.Invoke;
        }

        if (sendAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, sendAttribute))
        {
            return HubBoundaryKind.Send;
        }

        if (streamAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, streamAttribute))
        {
            return HubBoundaryKind.Stream;
        }

        return null;
    }

    static bool TryGetLiteralMethodName(
        IMethodSymbol method,
        HubBoundaryKind boundary,
        out string hubMethodName,
        out Location? badLocation)
    {
        var attributeClass = boundary switch
        {
            HubBoundaryKind.Invoke => "HubInvokeAttribute",
            HubBoundaryKind.Send => "HubSendAttribute",
            HubBoundaryKind.Stream => "HubStreamAttribute",
            _ => throw new ArgumentOutOfRangeException(nameof(boundary)),
        };

        AttributeData? attribute = null;
        foreach (var candidate in method.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == attributeClass)
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveMethodName(attribute, method.Name, out hubMethodName, out badLocation);
    }

    static bool TryGetLiteralMethodNameFromProperty(
        IPropertySymbol property,
        out string hubMethodName,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in property.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == "HubOnAttribute")
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveMethodName(attribute, property.Name, out hubMethodName, out badLocation);
    }

    static bool TryResolveMethodName(
        AttributeData? attribute,
        string fallbackName,
        out string hubMethodName,
        out Location? badLocation)
    {
        badLocation = null;
        hubMethodName = fallbackName;

        if (attribute is null)
        {
            return true;
        }

        if (attribute.ConstructorArguments.Length == 0
            || attribute.ConstructorArguments[0].IsNull)
        {
            return true;
        }

        if (attribute.ConstructorArguments[0].Value is string literal && !string.IsNullOrWhiteSpace(literal))
        {
            hubMethodName = literal;
            return true;
        }

        badLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        return false;
    }

    static bool HasMethodBoundaryOnProperty(
        IPropertySymbol property,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute) =>
        (invokeAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, invokeAttribute))
        || (sendAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, sendAttribute))
        || (streamAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, streamAttribute));

    static (List<string> declarations, List<string> names, bool hasCancellationToken) BuildParameters(
        IMethodSymbol method)
    {
        var declarations = new List<string>();
        var names = new List<string>();
        var hasCt = false;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            if (i == method.Parameters.Length - 1 && IsCancellationToken(parameter.Type))
            {
                hasCt = true;
                declarations.Add(
                    $"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)} = default");
                continue;
            }

            names.Add(IdentifierHelper.Escape(parameter.Name));
            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)}");
        }

        return (declarations, names, hasCt);
    }

    static bool IsCancellationToken(ITypeSymbol type) =>
        type.Name == "CancellationToken"
        && type.ContainingNamespace?.ToDisplayString() == "System.Threading";

    static bool IsUnsupportedStreamingParameter(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var def = named.OriginalDefinition.ToDisplayString(DisplayFormat);
        return def is "global::System.Collections.Generic.IAsyncEnumerable<T>"
            or "global::System.Threading.Channels.ChannelReader<T>";
    }
}
