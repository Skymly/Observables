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
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
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

        var interfaces = new List<PostgresInterfaceModel>();

        foreach (var marked in markedInterfaces)
        {
            var ifaceSymbol = marked.InterfaceSymbol;
            var nullable = marked.Nullability;

            var members = new List<PostgresMemberModel>();
            foreach (var member in marked.PublicInstanceMembers)
            {

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
        List<PostgresMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (listenAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, listenAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    method.Locations.FirstOrDefault(),
                    method.Name));
            return;
        }

        if (notifyAttribute is null || !IoProxyInterfaceWalk.HasAttribute(method, notifyAttribute))
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
                out var payloadParameterName,
                out var payloadTypeDisplay,
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
                payloadParameterName,
                payloadTypeDisplay));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        CSharpCompilation compilation,
        INamedTypeSymbol? notifyAttribute,
        INamedTypeSymbol? listenAttribute,
        INamedTypeSymbol? observableType,
        List<PostgresMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (notifyAttribute is not null && IoProxyInterfaceWalk.HasAttribute(property, notifyAttribute))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.MemberShapeMismatch,
                    property.Locations.FirstOrDefault(),
                    property.Name));
            return;
        }

        if (listenAttribute is null || !IoProxyInterfaceWalk.HasAttribute(property, listenAttribute))
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
                null,
                null));
    }

    static bool TryResolveNotifyParameters(
        IMethodSymbol method,
        out string? payloadParameterName,
        out string? payloadTypeDisplay,
        out Location? badLocation)
    {
        payloadParameterName = null;
        payloadTypeDisplay = null;
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
            payloadParameterName = IdentifierHelper.Escape(payloadCandidates[0].Name);
            payloadTypeDisplay = payloadCandidates[0].Type.ToDisplayString(DisplayFormat);
        }

        return true;
    }

    static bool IsValidChannelName(string channel) =>
        channel.Length is > 0 and <= 63 && ChannelNameRegex.IsMatch(channel);
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
