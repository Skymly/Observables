using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Mqtt.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    static readonly Regex PlaceholderRegex = new(@"\{([^}/]+)\}", RegexOptions.Compiled);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateMqttStubs(
        CSharpCompilation compilation,
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken)
    {
        var mqttAttribute = compilation.GetTypeByMetadataName("Observables.Mqtt.MqttAttribute");
        var publishAttribute = compilation.GetTypeByMetadataName("Observables.Mqtt.MqttPublishAttribute");
        var subscribeAttribute = compilation.GetTypeByMetadataName("Observables.Mqtt.MqttSubscribeAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        return IoProxyModelAssembly.Parse<MqttMemberModel, MqttInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
            cancellationToken,
            coreReferenced: mqttAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.MqttCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<MqttInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) => TryAddMethod(
                method,
                marked.InterfaceSymbol,
                compilation,
                publishAttribute,
                subscribeAttribute,
                observableType,
                unitType,
                members,
                diagnostics),
            tryAddProperty: (marked, property, members, diagnostics) => TryAddProperty(
                property,
                compilation,
                publishAttribute,
                subscribeAttribute,
                observableType,
                members,
                diagnostics),
            createInterface: static (marked, className, members) => new MqttInterfaceModel(
                $"{className}.Mqtt.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.Mqtt"),
                members,
                marked.Nullability),
            createContext: static interfaces => new ContextGenerationModel(interfaces));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? publishAttribute,
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<MqttMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (subscribeAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, subscribeAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        if (publishAttribute is null || !IoProxyInterfaceWalk.HasAttribute(method, publishAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidMqttMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryGetLiteralTopicTemplate(method, "MqttPublishAttribute", out var topicTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidMqttMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryParseTopicPlaceholders(topicTemplate, method, out var topicParameterNames, out var unsupportedLocation))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedMqttOption,
                    unsupportedLocation ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.Mqtt.Reactive.SystemReactiveMqttAdapter",
                observableType,
                unitType,
                requiresUnitPayload: true,
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
            new MqttMemberModel(
                IdentifierHelper.Escape(method.Name),
                topicTemplate,
                MqttBoundaryKind.Publish,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                topicParameterNames.ToImmutableEquatableArray(),
                hasCt));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? publishAttribute,
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        List<MqttMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (publishAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, publishAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (subscribeAttribute is null || !IoProxyInterfaceWalk.HasAttribute(property, subscribeAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidMqttMember,
                        property.Locations.FirstOrDefault(),
                        property.ContainingType.Name,
                        property.Name));
            }

            return;
        }

        if (!TryGetLiteralTopicTemplateFromProperty(property, out var topicTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidMqttMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (PlaceholderRegex.IsMatch(topicTemplate))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedMqttOption,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.Mqtt.Reactive.SystemReactiveMqttAdapter",
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
            new MqttMemberModel(
                IdentifierHelper.Escape(property.Name),
                topicTemplate,
                MqttBoundaryKind.Subscribe,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                false));
    }

    static bool TryParseTopicPlaceholders(
        string topicTemplate,
        IMethodSymbol method,
        out List<string> topicParameterNames,
        out Location? badLocation)
    {
        topicParameterNames = new List<string>();
        badLocation = null;
        var matches = PlaceholderRegex.Matches(topicTemplate);
        foreach (Match match in matches)
        {
            topicParameterNames.Add(match.Groups[1].Value);
        }

        var methodParamNames = new HashSet<string>(
            method.Parameters
                .Where(static p => !IsCancellationToken(p.Type))
                .Select(static p => p.Name),
            StringComparer.Ordinal);

        foreach (var name in topicParameterNames)
        {
            if (!methodParamNames.Contains(name))
            {
                badLocation = method.Locations.FirstOrDefault();
                return false;
            }
        }

        if (methodParamNames.Count != topicParameterNames.Count)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        return true;
    }

    static bool TryGetLiteralTopicTemplate(
        IMethodSymbol method,
        string attributeClassName,
        out string topicTemplate,
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

        return TryResolveTopicTemplate(attribute, method.Name, out topicTemplate, out badLocation);
    }

    static bool TryGetLiteralTopicTemplateFromProperty(
        IPropertySymbol property,
        out string topicTemplate,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in property.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == "MqttSubscribeAttribute")
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveTopicTemplate(attribute, property.Name, out topicTemplate, out badLocation);
    }

    static bool TryResolveTopicTemplate(
        AttributeData? attribute,
        string fallbackName,
        out string topicTemplate,
        out Location? badLocation)
    {
        badLocation = null;
        topicTemplate = fallbackName;

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
            topicTemplate = literal;
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
