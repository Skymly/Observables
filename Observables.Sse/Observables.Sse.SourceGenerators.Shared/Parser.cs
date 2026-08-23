using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

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
        var diagnostics = new List<Diagnostic>();
        var sseAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseAttribute");
        if (sseAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.SseCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<SseInterfaceModel>()));
        }

        var eventAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseEventAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);

        var interfaces = new List<SseInterfaceModel>();

        foreach (var marked in markedInterfaces)
        {
            var ifaceSymbol = marked.InterfaceSymbol;
            var nullable = marked.Nullability;

            var members = new List<SseMemberModel>();
            foreach (var member in marked.PublicInstanceMembers)
            {

                switch (member)
                {
                    case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                        TryAddMethod(method, ifaceSymbol, eventAttribute, diagnostics);
                        break;
                    case IPropertySymbol property:
                        TryAddProperty(
                            property,
                            ifaceSymbol,
                            compilation,
                            eventAttribute,
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
                new SseInterfaceModel(
                    $"{className}.Sse.g.cs",
                    className,
                    ifaceSymbol.ToDisplayString(DisplayFormat),
                    BackendTokens.QualifyGeneratedNamespace("Observables.Sse"),
                    members.ToImmutableEquatableArray(),
                    nullable));
        }

        return (diagnostics, new ContextGenerationModel(interfaces.ToImmutableEquatableArray()));
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
        List<SseMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (eventAttribute is null || !IoProxyInterfaceWalk.HasAttribute(property, eventAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidSseMember,
                        property.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        property.Name));
            }

            return;
        }

        var eventName = GetEventName(property) ?? "message";

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
                resultType));
    }

    static string? GetEventName(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "SseEventAttribute")
            {
                if (attr.ConstructorArguments.Length > 0
                    && attr.ConstructorArguments[0].Value is string s
                    && !string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }

                return null;
            }
        }

        return null;
    }

}
