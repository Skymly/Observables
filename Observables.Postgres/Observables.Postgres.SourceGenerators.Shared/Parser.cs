using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Postgres.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    static readonly Regex ChannelNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GeneratePostgresStubs(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var postgresAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.PostgresAttribute");
        if (postgresAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PostgresCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<PostgresInterfaceModel>()));
        }

        var notifyAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.NotifyAttribute");
        var listenAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.ListenAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);

        var interfaces = new List<PostgresInterfaceModel>();

        foreach (var group in candidateInterfaces.GroupBy(static i => i.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var ifaceSyntax in group)
            {
                if (semanticModel.GetDeclaredSymbol(ifaceSyntax, cancellationToken) is not INamedTypeSymbol ifaceSymbol)
                {
                    continue;
                }

                if (!HasAttribute(ifaceSymbol, postgresAttribute))
                {
                    continue;
                }

                var nullable = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(ifaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = new List<PostgresMemberModel>();
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
                                notifyAttribute,
                                listenAttribute,
                                observableType,
                                unitType,
                                stringType,
                                members,
                                diagnostics);
                            break;
                        case IPropertySymbol property:
                            TryAddProperty(
                                property,
                                compilation,
                                notifyAttribute,
                                listenAttribute,
                                observableType,
                                stringType,
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
                    new PostgresInterfaceModel(
                        $"{className}.Postgres.g.cs",
                        className,
                        ifaceSymbol.ToDisplayString(DisplayFormat),
                        BackendTokens.QualifyGeneratedNamespace("Observables.Postgres"),
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
        INamedTypeSymbol? notifyAttribute,
        INamedTypeSymbol? listenAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        INamedTypeSymbol stringType,
        List<PostgresMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (listenAttribute is not null && HasAttribute(method, listenAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        if (notifyAttribute is null || !HasAttribute(method, notifyAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPostgresMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryGetLiteralChannel(method, "NotifyAttribute", method.Name, out var channel, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPostgresMember,
                    location ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!IsValidChannelName(channel))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedPostgresOption,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryResolveNotifyParameters(
                method,
                stringType,
                out var payloadParameterName,
                out var unsupportedLocation))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedPostgresOption,
                    unsupportedLocation ?? method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.Postgres.Reactive.SystemReactivePostgresAdapter",
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
            new PostgresMemberModel(
                IdentifierHelper.Escape(method.Name),
                channel,
                PostgresBoundaryKind.Notify,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                hasCt,
                payloadParameterName));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? notifyAttribute,
        INamedTypeSymbol? listenAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol stringType,
        List<PostgresMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (notifyAttribute is not null && HasAttribute(property, notifyAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (listenAttribute is null || !HasAttribute(property, listenAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidPostgresMember,
                        property.Locations.FirstOrDefault(),
                        property.ContainingType.Name,
                        property.Name));
            }

            return;
        }

        if (!TryGetLiteralChannel(property, "ListenAttribute", property.Name, out var channel, out var location))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPostgresMember,
                    location ?? property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (!IsValidChannelName(channel))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedPostgresOption,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                property.Type,
                compilation,
                "Observables.Postgres.Reactive.SystemReactivePostgresAdapter",
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

        if (property.Type is not INamedTypeSymbol listenObservable
            || !listenObservable.IsGenericType
            || !IsStringType(listenObservable.TypeArguments[0], stringType))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedReturnType,
                    property.Locations.FirstOrDefault(),
                    returnDisplay));
            return;
        }

        members.Add(
            new PostgresMemberModel(
                IdentifierHelper.Escape(property.Name),
                channel,
                PostgresBoundaryKind.Listen,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                false,
                null));
    }

    static bool TryResolveNotifyParameters(
        IMethodSymbol method,
        INamedTypeSymbol stringType,
        out string? payloadParameterName,
        out Location? badLocation)
    {
        payloadParameterName = null;
        badLocation = null;
        var payloadCandidates = new List<IParameterSymbol>();

        foreach (var parameter in method.Parameters)
        {
            if (IsCancellationToken(parameter.Type))
            {
                continue;
            }

            payloadCandidates.Add(parameter);
        }

        if (payloadCandidates.Count > 1)
        {
            badLocation = method.Locations.FirstOrDefault();
            return false;
        }

        if (payloadCandidates.Count == 1)
        {
            if (!IsStringType(payloadCandidates[0].Type, stringType))
            {
                badLocation = payloadCandidates[0].Locations.FirstOrDefault()
                    ?? method.Locations.FirstOrDefault();
                return false;
            }

            payloadParameterName = IdentifierHelper.Escape(payloadCandidates[0].Name);
        }

        return true;
    }

    static bool IsStringType(ITypeSymbol? type, INamedTypeSymbol stringType) =>
        type is not null
        && (type.SpecialType == SpecialType.System_String
            || SymbolEqualityComparer.Default.Equals(type.WithNullableAnnotation(NullableAnnotation.None), stringType));

    static bool IsValidChannelName(string channel) =>
        channel.Length is > 0 and <= 63 && ChannelNameRegex.IsMatch(channel);

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

    static bool TryGetLiteralChannel(
        ISymbol member,
        string attributeClassName,
        string fallbackName,
        out string channel,
        out Location? badLocation)
    {
        AttributeData? attribute = null;
        foreach (var candidate in member.GetAttributes())
        {
            if (candidate.AttributeClass?.Name == attributeClassName)
            {
                attribute = candidate;
                break;
            }
        }

        badLocation = null;
        channel = fallbackName;

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
            channel = text;
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
