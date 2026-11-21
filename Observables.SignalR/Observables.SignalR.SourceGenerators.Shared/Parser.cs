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
        CancellationToken cancellationToken,
        bool failSafeProbe = false)
    {
        var hubAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubAttribute");
        var invokeAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubInvokeAttribute");
        var sendAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubSendAttribute");
        var streamAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubStreamAttribute");
        var onAttribute = compilation.GetTypeByMetadataName("Observables.SignalR.HubOnAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        var incompleteProxies = new HashSet<string>(StringComparer.Ordinal);
        return IoProxyModelAssembly.Parse<HubMemberModel, HubInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
            cancellationToken,
            coreReferenced: hubAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.SignalRCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<HubInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) => AddMember(
                marked, method, members,
                ParseMethod(method, marked.InterfaceSymbol, compilation, invokeAttribute, sendAttribute,
                    streamAttribute, onAttribute, observableType, unitType, diagnostics)),
            tryAddProperty: (marked, property, members, diagnostics) => AddMember(
                marked, property, members,
                ParseProperty(property, compilation, invokeAttribute, sendAttribute, streamAttribute,
                    onAttribute, observableType, diagnostics)),
            createInterface: (marked, className, members) => new HubInterfaceModel(
                GetHintName(marked),
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.SignalR"),
                members,
                marked.Nullability,
                GetHubName(marked.InterfaceSymbol, hubAttribute)),
            createContext: interfaces => new ContextGenerationModel(
                incompleteProxies.Count == 0
                    ? interfaces
                    : interfaces.AsArray().Where(proxy => !incompleteProxies.Contains(proxy.FileName))
                        .ToImmutableEquatableArray()),
            onMarkedInterface: marked =>
            {
                if (failSafeProbe && marked.Syntax.Identifier.ValueText == "IInternalErrorProbe")
                {
                    throw new InvalidOperationException("fail-safe probe");
                }
            },
            tryAddOther: (marked, member, members, diagnostics) =>
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidHubMember,
                        member.Locations.FirstOrDefault(),
                        marked.InterfaceSymbol.Name,
                        member.Name));
                AddMember(marked, member, members, null);
            });

        void AddMember(MarkedInterfaceContext marked, ISymbol contract, List<HubMemberModel> members, HubMemberModel? model)
        {
            if (model is not null)
            {
                members.Add(model);
            }
            else if (contract.IsAbstract)
            {
                // The valid members alone cannot implement the contract.
                incompleteProxies.Add(GetHintName(marked));
            }
        }
    }

    static string GetHintName(MarkedInterfaceContext marked) =>
        IoProxyModelAssembly.GeneratedHintName(marked.InterfaceSymbol, "SignalR");

    /// <summary>
    /// Reads the name off <c>[Hub(hubName)]</c>. A null or blank name means the interface opts out of
    /// named resolution and stays reachable only through <c>HubService.For&lt;T&gt;(HubConnection)</c>.
    /// </summary>
    static string? GetHubName(INamedTypeSymbol interfaceSymbol, INamedTypeSymbol? hubAttribute)
    {
        if (hubAttribute is null)
        {
            return null;
        }

        foreach (var attribute in interfaceSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, hubAttribute))
            {
                continue;
            }

            return attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string name
                && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : null;
        }

        return null;
    }

    static HubMemberModel? ParseMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute,
        INamedTypeSymbol? onAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<Diagnostic> diagnostics)
    {
        if (onAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, onAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return null;
        }

        var match = FindMethodBoundary(method, invokeAttribute, sendAttribute, streamAttribute, out var conflict);
        if (match is not { } boundary)
        {
            var memberLocation = method.Locations.FirstOrDefault();
            diagnostics.Add(conflict
                ? Diagnostic.Create(DiagnosticDescriptors.MultipleHubBoundaries, memberLocation, method.Name)
                : Diagnostic.Create(DiagnosticDescriptors.InvalidHubMember, memberLocation, ifaceSymbol.Name, method.Name));
            return null;
        }

        if (!TryResolveMethodName(boundary.Attribute, method.Name, out var hubMethodName, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return null;
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
                return null;
            }
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.SignalR.Reactive.SystemReactiveSignalRAdapter",
                observableType,
                unitType,
                requiresUnitPayload: boundary.Kind == HubBoundaryKind.Send,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                method.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return null;
        }

        if (IdentifierHelper.HasNonTrailingCancellationToken(method))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return null;
        }

        var (declarations, names, ctName) = BuildParameters(method);
        return new HubMemberModel(
            IdentifierHelper.Escape(method.Name),
            hubMethodName,
            boundary.Kind,
            false,
            returnDisplay,
            resultType,
            declarations.ToImmutableEquatableArray(),
            names.ToImmutableEquatableArray(),
            ctName);
    }

    static HubMemberModel? ParseProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute,
        INamedTypeSymbol? onAttribute,
        INamedTypeSymbol? observableType,
        List<Diagnostic> diagnostics)
    {
        if (HasMethodBoundaryOnProperty(property, invokeAttribute, sendAttribute, streamAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return null;
        }

        var attribute = FindAttribute(property, onAttribute);
        if (attribute is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return null;
        }

        if (!TryResolveMethodName(attribute, property.Name, out var hubMethodName, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidHubMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return null;
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
            return null;
        }

        return new HubMemberModel(
            IdentifierHelper.Escape(property.Name),
            hubMethodName,
            HubBoundaryKind.On,
            true,
            returnDisplay,
            resultType,
            ImmutableEquatableArray.Empty<string>(),
            ImmutableEquatableArray.Empty<string>(),
            null);
    }

    static (HubBoundaryKind Kind, AttributeData Attribute)? FindMethodBoundary(
        IMethodSymbol method,
        INamedTypeSymbol? invokeAttribute,
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? streamAttribute,
        out bool conflict)
    {
        conflict = false;
        (HubBoundaryKind Kind, AttributeData Attribute)? selected = null;
        foreach (var attribute in method.GetAttributes())
        {
            var type = attribute.AttributeClass;
            var kind = type switch
            {
                not null when SymbolEqualityComparer.Default.Equals(type, invokeAttribute) => HubBoundaryKind.Invoke,
                not null when SymbolEqualityComparer.Default.Equals(type, sendAttribute) => HubBoundaryKind.Send,
                not null when SymbolEqualityComparer.Default.Equals(type, streamAttribute) => HubBoundaryKind.Stream,
                _ => (HubBoundaryKind?)null,
            };
            if (kind is null)
                continue;

            if (selected.HasValue)
            {
                conflict = true;
                return null;
            }

            selected = (kind.Value, attribute);
        }

        return selected;
    }

    static AttributeData? FindAttribute(ISymbol member, INamedTypeSymbol? attributeType) =>
        attributeType is null
            ? null
            : member.GetAttributes().FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));

    static bool TryResolveMethodName(
        AttributeData attribute,
        string fallbackName,
        out string hubMethodName,
        out Location? badLocation)
    {
        badLocation = null;
        hubMethodName = fallbackName;

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
