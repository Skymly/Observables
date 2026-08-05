using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Redis.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    static readonly Regex PlaceholderRegex = new(@"\{([^}/]+)\}", RegexOptions.Compiled);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateRedisStubs(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var redisAttribute = compilation.GetTypeByMetadataName("Observables.Redis.RedisAttribute");
        if (redisAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.RedisCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<RedisInterfaceModel>()));
        }

        var publishAttribute = compilation.GetTypeByMetadataName("Observables.Redis.RedisPublishAttribute");
        var subscribeAttribute = compilation.GetTypeByMetadataName("Observables.Redis.RedisSubscribeAttribute");
        var redisMessageType = compilation.GetTypeByMetadataName("Observables.Redis.RedisMessage`1");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        var interfaces = new List<RedisInterfaceModel>();

        foreach (var group in candidateInterfaces.GroupBy(static i => i.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var ifaceSyntax in group)
            {
                if (semanticModel.GetDeclaredSymbol(ifaceSyntax, cancellationToken) is not INamedTypeSymbol ifaceSymbol)
                {
                    continue;
                }

                if (!HasAttribute(ifaceSymbol, redisAttribute))
                {
                    continue;
                }

                var nullable = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(ifaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = new List<RedisMemberModel>();
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
                                redisMessageType,
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
                    new RedisInterfaceModel(
                        $"{className}.Redis.g.cs",
                        className,
                        ifaceSymbol.ToDisplayString(DisplayFormat),
                        BackendTokens.QualifyGeneratedNamespace("Observables.Redis"),
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
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<RedisMemberModel> members,
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
        if (!hasPublish)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRedisMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryGetLiteralChannelTemplate(method, "RedisPublishAttribute", out var channelTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRedisMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (channelTemplate.IndexOf('*') >= 0 || channelTemplate.IndexOf('?') >= 0)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedRedisOption,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryResolveMethodParameters(
                channelTemplate,
                method,
                out var channelParameterNames,
                out var payloadParameterName,
                out var payloadTypeDisplay,
                out var unsupportedLocation))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedRedisOption,
                    unsupportedLocation ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.Redis.Reactive.SystemReactiveRedisAdapter",
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
            new RedisMemberModel(
                IdentifierHelper.Escape(method.Name),
                channelTemplate,
                RedisBoundaryKind.Publish,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                channelParameterNames.ToImmutableEquatableArray(),
                hasCt,
                payloadParameterName,
                payloadTypeDisplay,
                IsPatternSubscribe: false,
                UseEnvelope: false));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? publishAttribute,
        INamedTypeSymbol? subscribeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? redisMessageType,
        List<RedisMemberModel> members,
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

        if (subscribeAttribute is null || !HasAttribute(property, subscribeAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidRedisMember,
                        property.Locations.FirstOrDefault(),
                        property.ContainingType.Name,
                        property.Name));
            }

            return;
        }

        if (!TryGetLiteralChannelTemplateFromProperty(property, out var channelTemplate, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidRedisMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (PlaceholderRegex.IsMatch(channelTemplate))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedRedisOption,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        var isPattern = channelTemplate.IndexOf('*') >= 0 || channelTemplate.IndexOf('?') >= 0;

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.Redis.Reactive.SystemReactiveRedisAdapter",
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

        var useEnvelope = TryUnwrapRedisMessage(
            property.Type,
            redisMessageType,
            ref resultType);

        members.Add(
            new RedisMemberModel(
                IdentifierHelper.Escape(property.Name),
                channelTemplate,
                RedisBoundaryKind.Subscribe,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                false,
                null,
                null,
                IsPatternSubscribe: isPattern,
                UseEnvelope: useEnvelope));
    }

    static bool TryUnwrapRedisMessage(
        ITypeSymbol observableReturnType,
        INamedTypeSymbol? redisMessageType,
        ref string resultTypeDisplay)
    {
        if (redisMessageType is null
            || observableReturnType is not INamedTypeSymbol named
            || !named.IsGenericType
            || named.TypeArguments.Length != 1
            || named.TypeArguments[0] is not INamedTypeSymbol element
            || !element.IsGenericType
            || element.TypeArguments.Length != 1
            || !SymbolEqualityComparer.Default.Equals(element.OriginalDefinition, redisMessageType))
        {
            return false;
        }

        resultTypeDisplay = element.TypeArguments[0].ToDisplayString(DisplayFormat);
        return true;
    }

    static bool TryResolveMethodParameters(
        string channelTemplate,
        IMethodSymbol method,
        out List<string> channelParameterNames,
        out string? payloadParameterName,
        out string? payloadTypeDisplay,
        out Location? badLocation)
    {
        channelParameterNames = new List<string>();
        payloadParameterName = null;
        payloadTypeDisplay = null;
        badLocation = null;

        foreach (Match match in PlaceholderRegex.Matches(channelTemplate))
        {
            channelParameterNames.Add(match.Groups[1].Value);
        }

        var channelParamSet = new HashSet<string>(channelParameterNames, StringComparer.Ordinal);
        var payloadCandidates = new List<IParameterSymbol>();

        foreach (var parameter in method.Parameters)
        {
            if (IsCancellationToken(parameter.Type))
            {
                continue;
            }

            if (channelParamSet.Contains(parameter.Name))
            {
                continue;
            }

            payloadCandidates.Add(parameter);
        }

        foreach (var name in channelParameterNames)
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

        if (payloadCandidates.Count == 1)
        {
            payloadParameterName = IdentifierHelper.Escape(payloadCandidates[0].Name);
            payloadTypeDisplay = payloadCandidates[0].Type.ToDisplayString(DisplayFormat);
        }

        return true;
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

    static bool TryGetLiteralChannelTemplate(
        IMethodSymbol method,
        string attributeClassName,
        out string channelTemplate,
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

        return TryResolveChannelTemplate(attribute, method.Name, out channelTemplate, out badLocation);
    }

    static bool TryGetLiteralChannelTemplateFromProperty(
        IPropertySymbol property,
        out string channelTemplate,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in property.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == "RedisSubscribeAttribute")
            {
                attribute = candidate;
                break;
            }
        }

        return TryResolveChannelTemplate(attribute, property.Name, out channelTemplate, out badLocation);
    }

    static bool TryResolveChannelTemplate(
        AttributeData? attribute,
        string fallbackName,
        out string channelTemplate,
        out Location? badLocation)
    {
        badLocation = null;
        channelTemplate = fallbackName;

        if (attribute is null)
        {
            return true;
        }

        var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
        var argument = attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault();
        if (argument is null)
        {
            return true;
        }

        if (argument.Expression is LiteralExpressionSyntax literal
            && literal.Token.IsKind(SyntaxKind.StringLiteralToken)
            && literal.Token.Value is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            channelTemplate = text;
            return true;
        }

        if (argument.Expression is LiteralExpressionSyntax nullLiteral
            && nullLiteral.Token.IsKind(SyntaxKind.NullKeyword))
        {
            return true;
        }

        badLocation = argument.GetLocation();
        return false;
    }

    static (List<string> declarations, bool hasCancellationToken) BuildParameters(IMethodSymbol method)
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
