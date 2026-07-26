using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Nats.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    static readonly Regex PlaceholderRegex = new(@"\{([^}/]+)\}", RegexOptions.Compiled);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateNatsStubs(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var mqttAttribute = compilation.GetTypeByMetadataName("Observables.Nats.NatsAttribute");
        if (mqttAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.NatsCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<NatsInterfaceModel>()));
        }

        var publishAttribute = compilation.GetTypeByMetadataName("Observables.Nats.NatsPublishAttribute");
        var requestAttribute = compilation.GetTypeByMetadataName("Observables.Nats.NatsRequestAttribute");
        var subscribeAttribute = compilation.GetTypeByMetadataName("Observables.Nats.NatsSubscribeAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        var interfaces = new List<NatsInterfaceModel>();

        foreach (var group in candidateInterfaces.GroupBy(static i => i.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var ifaceSyntax in group)
            {
                if (semanticModel.GetDeclaredSymbol(ifaceSyntax, cancellationToken) is not INamedTypeSymbol ifaceSymbol)
                {
                    continue;
                }

                if (!HasNatsAttribute(ifaceSymbol, mqttAttribute))
                {
                    continue;
                }

                var nullable = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(ifaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = new List<NatsMemberModel>();
                foreach (var member in ifaceSymbol.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
                    {
                        continue;
                    }

                    switch (member)
                    {
                        case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                            TryAddMethod(
                                method,
                                ifaceSymbol,
                                compilation,
                                publishAttribute,
                                requestAttribute,
                                subscribeAttribute,
                                observableType,
                                unitType,
                                members,
                                diagnostics);
                            break;
                        case IPropertySymbol property:
                            TryAddProperty(
                                property,
                                compilation,
                                publishAttribute,
                                subscribeAttribute,
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
                    new NatsInterfaceModel(
                        $"{className}.Nats.g.cs",
                        className,
                        ifaceSymbol.ToDisplayString(DisplayFormat),
                        BackendTokens.QualifyGeneratedNamespace("Observables.Nats"),
                        members.ToImmutableEquatableArray(),
                        nullable));
            }
        }

        return (diagnostics, new ContextGenerationModel(interfaces.ToImmutableEquatableArray()));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? publishAttribute,
        INamedTypeSymbol? requestAttribute,
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<NatsMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (subscribeAttribute is not null && HasAttribute(method, subscribeAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        var hasPublish = publishAttribute is not null && HasAttribute(method, publishAttribute);
        var hasRequest = requestAttribute is not null && HasAttribute(method, requestAttribute);

        if (hasPublish && hasRequest)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        if (!hasPublish && !hasRequest)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidNatsMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        var attributeName = hasRequest ? "NatsRequestAttribute" : "NatsPublishAttribute";
        var boundary = hasRequest ? NatsBoundaryKind.Request : NatsBoundaryKind.Publish;

        if (!TryGetLiteralSubjectTemplate(method, attributeName, out var subjectTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidNatsMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryResolveMethodParameters(
                subjectTemplate,
                method,
                allowPayloadParameter: true,
                requirePayloadParameter: hasRequest,
                out var subjectParameterNames,
                out var payloadParameterName,
                out var payloadTypeDisplay,
                out var unsupportedLocation))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedNatsOption,
                    unsupportedLocation ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.Nats.Reactive.SystemReactiveNatsAdapter",
                observableType,
                unitType,
                requiresUnitPayload: boundary == NatsBoundaryKind.Publish,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                method.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return;
        }

        var (declarations, hasCt) = BuildParameters(method);
        members.Add(
            new NatsMemberModel(
                IdentifierHelper.Escape(method.Name),
                subjectTemplate,
                boundary,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                subjectParameterNames.ToImmutableEquatableArray(),
                hasCt,
                payloadParameterName,
                payloadTypeDisplay));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? publishAttribute,
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        List<NatsMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (publishAttribute is not null && HasAttribute(property, publishAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (compilation.GetTypeByMetadataName("Observables.Nats.NatsRequestAttribute") is { } requestAttribute
            && HasAttribute(property, requestAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (subscribeAttribute is null || !HasAttribute(property, subscribeAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidNatsMember,
                        property.Locations.FirstOrDefault(),
                        property.ContainingType.Name,
                        property.Name));
            }

            return;
        }

        if (!TryGetLiteralSubjectTemplateFromProperty(property, out var subjectTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidNatsMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (PlaceholderRegex.IsMatch(subjectTemplate))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedNatsOption,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.Nats.Reactive.SystemReactiveNatsAdapter",
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
            new NatsMemberModel(
                IdentifierHelper.Escape(property.Name),
                subjectTemplate,
                NatsBoundaryKind.Subscribe,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                false,
                null,
                null));
    }

    static bool TryResolveMethodParameters(
        string subjectTemplate,
        IMethodSymbol method,
        bool allowPayloadParameter,
        bool requirePayloadParameter,
        out List<string> subjectParameterNames,
        out string? payloadParameterName,
        out string? payloadTypeDisplay,
        out Location? badLocation)
    {
        subjectParameterNames = new List<string>();
        payloadParameterName = null;
        payloadTypeDisplay = null;
        badLocation = null;

        foreach (Match match in PlaceholderRegex.Matches(subjectTemplate))
        {
            subjectParameterNames.Add(match.Groups[1].Value);
        }

        var subjectParamSet = new HashSet<string>(subjectParameterNames, StringComparer.Ordinal);
        var payloadCandidates = new List<IParameterSymbol>();

        foreach (var parameter in method.Parameters)
        {
            if (IsCancellationToken(parameter.Type))
            {
                continue;
            }

            if (subjectParamSet.Contains(parameter.Name))
            {
                continue;
            }

            payloadCandidates.Add(parameter);
        }

        foreach (var name in subjectParameterNames)
        {
            var found = method.Parameters.Any(p => p.Name == name && !IsCancellationToken(p.Type));
            if (!found)
            {
                badLocation = method.Locations.FirstOrDefault();
                return false;
            }
        }

        if (payloadCandidates.Count > 1)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        if (requirePayloadParameter && payloadCandidates.Count != 1)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        if (!allowPayloadParameter && payloadCandidates.Count > 0)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        if (payloadCandidates.Count == 1)
        {
            payloadParameterName = IdentifierHelper.Escape(payloadCandidates[0].Name);
            payloadTypeDisplay = payloadCandidates[0].Type.ToDisplayString(DisplayFormat);
        }

        return true;
    }

    static bool TryParseSubjectPlaceholders(
        string subjectTemplate,
        IMethodSymbol method,
        out List<string> subjectParameterNames,
        out Location? badLocation)
    {
        subjectParameterNames = new List<string>();
        badLocation = null;
        var matches = PlaceholderRegex.Matches(subjectTemplate);
        foreach (Match match in matches)
        {
            subjectParameterNames.Add(match.Groups[1].Value);
        }

        var methodParamNames = new HashSet<string>(
            method.Parameters
                .Where(static p => !IsCancellationToken(p.Type))
                .Select(static p => p.Name),
            StringComparer.Ordinal);

        foreach (var name in subjectParameterNames)
        {
            if (!methodParamNames.Contains(name))
            {
                badLocation = method.Locations.FirstOrDefault();
                return false;
            }
        }

        if (methodParamNames.Count != subjectParameterNames.Count)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        return true;
    }

    static bool HasNatsAttribute(INamedTypeSymbol ifaceSymbol, INamedTypeSymbol mqttAttribute)
    {
        foreach (var attribute in ifaceSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, mqttAttribute))
            {
                return true;
            }
        }

        return false;
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

    static bool TryGetLiteralSubjectTemplate(
        IMethodSymbol method,
        string attributeClassName,
        out string subjectTemplate,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in method.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == attributeClassName)
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveSubjectTemplate(attribute, method.Name, out subjectTemplate, out badLocation);
    }

    static bool TryGetLiteralSubjectTemplateFromProperty(
        IPropertySymbol property,
        out string subjectTemplate,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in property.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == "NatsSubscribeAttribute")
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveSubjectTemplate(attribute, property.Name, out subjectTemplate, out badLocation);
    }

    static bool TryResolveSubjectTemplate(
        AttributeData? attribute,
        string fallbackName,
        out string subjectTemplate,
        out Location? badLocation)
    {
        badLocation = null;
        subjectTemplate = fallbackName;

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
            subjectTemplate = literal;
            return true;
        }

        badLocation = attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        return false;
    }

    static (List<string> declarations, bool hasCancellationToken) BuildParameters(
        IMethodSymbol method)
    {
        var declarations = new List<string>();
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

            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)}");
        }

        return (declarations, hasCt);
    }

    static bool IsCancellationToken(ITypeSymbol type) =>
        type.Name == "CancellationToken"
        && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
}
