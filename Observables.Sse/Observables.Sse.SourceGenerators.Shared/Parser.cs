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

#if SSE_R3
    const string ObservableMetadataName = "R3.Observable`1";
#else
    const string ObservableMetadataName = "System.IObservable`1";
#endif

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateSseStubs(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
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
        var observableType = compilation.GetTypeByMetadataName(ObservableMetadataName);

        var interfaces = new List<SseInterfaceModel>();

        foreach (var group in candidateInterfaces.GroupBy(static i => i.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var ifaceSyntax in group)
            {
                if (semanticModel.GetDeclaredSymbol(ifaceSyntax, cancellationToken) is not INamedTypeSymbol ifaceSymbol)
                {
                    continue;
                }

                if (!HasAttribute(ifaceSymbol, sseAttribute))
                {
                    continue;
                }

                var nullable = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(ifaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = new List<SseMemberModel>();
                foreach (var member in ifaceSymbol.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
                    {
                        continue;
                    }

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
#if SSE_R3
                        "Observables.Sse.Generated",
#else
                        "Observables.Sse.Reactive.Generated",
#endif
                        members.ToImmutableEquatableArray(),
                        nullable));
            }
        }

        return (diagnostics, new ContextGenerationModel(interfaces.ToImmutableEquatableArray()));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        INamedTypeSymbol? eventAttribute,
        List<Diagnostic> diagnostics)
    {
        if (eventAttribute is not null && HasAttribute(method, eventAttribute))
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
        if (eventAttribute is null || !HasAttribute(property, eventAttribute))
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

        if (!TryParseObservableReturn(
                compilation,
                property.Type,
                observableType,
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

    static bool TryParseObservableReturn(
        CSharpCompilation compilation,
        ITypeSymbol returnType,
        INamedTypeSymbol? observableType,
        Location? location,
        List<Diagnostic> diagnostics,
        out string resultTypeDisplay,
        out string returnTypeDisplay) =>
        ObservableReturnTypeParser.TryParse(
            returnType,
            compilation,
#if SSE_R3
            isR3Generator: true,
#else
            isR3Generator: false,
#endif
            reactiveAdapterMetadataName: "Observables.Sse.Reactive.SystemReactiveSseAdapter",
            observableType,
            unitType: null,
            requiresUnitPayload: false,
            DiagnosticDescriptors.UnsupportedReturnType,
            DiagnosticDescriptors.SystemReactiveNotReferenced,
            location,
            diagnostics,
            out resultTypeDisplay,
            out returnTypeDisplay);

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

    static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
