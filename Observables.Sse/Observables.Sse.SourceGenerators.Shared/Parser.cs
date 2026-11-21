using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.Sse.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateSseStubs(
        CSharpCompilation compilation,
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken)
    {
        var sseAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseAttribute");
        var eventAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseEventAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);

        var propertyNamesRequiringExplicitImplementation = new Dictionary<INamedTypeSymbol, HashSet<string>>(
            SymbolEqualityComparer.Default);
        var contracts = ImmutableArray.CreateBuilder<MarkedInterfaceContext>(markedInterfaces.Length);
        foreach (var marked in markedInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var properties = new[] { marked.InterfaceSymbol }.Concat(marked.InterfaceSymbol.AllInterfaces)
                .SelectMany(contract => contract.GetMembers().OfType<IPropertySymbol>())
                .Where(property => property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic)
                .ToArray();

            var repeatedNames = new HashSet<string>(properties.GroupBy(property => property.Name, StringComparer.Ordinal)
                .Where(group => group.Skip(1).Any())
                .Select(group => group.Key), StringComparer.Ordinal);
            if (repeatedNames.Count == 0)
            {
                contracts.Add(marked);
                continue;
            }

            // The same name can denote different event streams in two interface declarations.
            propertyNamesRequiringExplicitImplementation.Add(marked.InterfaceSymbol, repeatedNames);
            contracts.Add(marked with
            {
                PublicInstanceMembers = marked.PublicInstanceMembers.Where(member => member is not IPropertySymbol)
                    .Concat(properties)
                    .ToImmutableArray(),
            });
        }

        return IoProxyModelAssembly.Parse<SseMemberModel, SseInterfaceModel, ContextGenerationModel>(
            contracts.MoveToImmutable(),
            cancellationToken,
            coreReferenced: sseAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.SseCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<SseInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) =>
                TryAddMethod(method, marked.InterfaceSymbol, eventAttribute, diagnostics),
            tryAddProperty: (marked, property, members, diagnostics) => TryAddProperty(
                property,
                marked.InterfaceSymbol,
                compilation,
                eventAttribute,
                observableType,
                propertyNamesRequiringExplicitImplementation.TryGetValue(marked.InterfaceSymbol, out var names)
                    && names.Contains(property.Name),
                members,
                diagnostics),
            createInterface: (marked, className, members) => new SseInterfaceModel(
                $"{marked.InterfaceSymbol.GetSafeHintName()}.Sse.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.Sse"),
                members,
                marked.Nullability,
                GetEndpointName(marked.InterfaceSymbol, sseAttribute)),
            createContext: static interfaces => new ContextGenerationModel(interfaces),
            tryAddOther: (marked, member, _, diagnostics) =>
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidSseMember,
                        member.Locations.FirstOrDefault(),
                        marked.InterfaceSymbol.Name,
                        member.Name)));
    }

    /// <summary>
    /// Reads the name off <c>[Sse(endpointName)]</c>. A null or blank name means the interface opts out of
    /// named resolution and stays reachable only through <c>SseService.For&lt;T&gt;(SseConnection)</c>.
    /// </summary>
    static string? GetEndpointName(INamedTypeSymbol interfaceSymbol, INamedTypeSymbol? sseAttribute)
    {
        if (sseAttribute is null)
        {
            return null;
        }

        foreach (var attribute in interfaceSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, sseAttribute))
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

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        INamedTypeSymbol? eventAttribute,
        List<Diagnostic> diagnostics)
    {
        if (eventAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, eventAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        diagnostics.Add(
            Diagnostic.Create(
                DiagnosticDescriptors.InvalidSseMember,
                method.Locations.FirstOrDefault(),
                ifaceSymbol.Name,
                method.Name));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? eventAttribute,
        INamedTypeSymbol? observableType,
        bool implementExplicitly,
        List<SseMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        var declaration = eventAttribute is null
            ? null
            : property.GetAttributes().FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventAttribute));
        if (declaration is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidSseMember,
                    property.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    property.Name));
            return;
        }

        var eventName = declaration.ConstructorArguments.Length > 0
            && declaration.ConstructorArguments[0].Value is string name
            && !string.IsNullOrWhiteSpace(name)
                ? name
                : "message";

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.Sse.Reactive.SystemReactiveSseAdapter",
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
            new SseMemberModel(
                IdentifierHelper.Escape(property.Name),
                eventName,
                SseBoundaryKind.Event,
                returnDisplay,
                resultType,
                implementExplicitly ? property.ContainingType.ToDisplayString(DisplayFormat) : null));
    }
}
