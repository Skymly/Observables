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
        var sseAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseAttribute");
        var eventAttribute = compilation.GetTypeByMetadataName("Observables.Sse.SseEventAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);

        return IoProxyModelAssembly.Parse<SseMemberModel, SseInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
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
                members,
                diagnostics),
            createInterface: static (marked, className, members) => new SseInterfaceModel(
                $"{className}.Sse.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.Sse"),
                members,
                marked.Nullability),
            createContext: static interfaces => new ContextGenerationModel(interfaces));
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
