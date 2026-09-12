using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;
using Observables.SourceGenerators.Shared.Extensions;

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
        var postgresAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.PostgresAttribute");
        var notifyAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.NotifyAttribute");
        var listenAttribute = compilation.GetTypeByMetadataName("Observables.Postgres.ListenAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(BackendTokens.UnitMetadataName);

        return IoProxyModelAssembly.Parse<PostgresMemberModel, PostgresInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
            cancellationToken,
            coreReferenced: postgresAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.PostgresCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<PostgresInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) => TryAddMethod(
                method,
                marked.InterfaceSymbol,
                compilation,
                notifyAttribute,
                listenAttribute,
                observableType,
                unitType,
                members,
                diagnostics),
            tryAddProperty: (marked, property, members, diagnostics) => TryAddProperty(
                property,
                compilation,
                notifyAttribute,
                listenAttribute,
                observableType,
                members,
                diagnostics),
            createInterface: static (marked, className, members) => new PostgresInterfaceModel(
                $"{marked.InterfaceSymbol.GetSafeHintName()}.Postgres.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.Postgres"),
                members,
                marked.Nullability),
            createContext: static interfaces => new ContextGenerationModel(interfaces));
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

        if (HasNonTrailingCancellationToken(method))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPostgresMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        var (declarations, ctName) = BuildParameters(method);
        members.Add(
            new PostgresMemberModel(
                IdentifierHelper.Escape(method.Name),
                channel,
                PostgresBoundaryKind.Notify,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                ctName,
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
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidPostgresMember,
                    property.Locations.FirstOrDefault(),
                    property.ContainingType.Name,
                    property.Name));
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
                null,
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

    static (List<string> declarations, string? cancellationTokenParameterName) BuildParameters(IMethodSymbol method)
    {
        var declarations = new List<string>();
        string? ctName = null;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            if (i == method.Parameters.Length - 1 && IsCancellationToken(parameter.Type))
            {
                ctName = IdentifierHelper.Escape(parameter.Name);
                declarations.Add(
                    $"{parameter.Type.ToDisplayString(DisplayFormat)} {ctName} = default");
                continue;
            }

            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)}");
        }

        return (declarations, ctName);
    }

    static bool HasNonTrailingCancellationToken(IMethodSymbol method)
    {
        for (var i = 0; i < method.Parameters.Length - 1; i++)
        {
            if (IsCancellationToken(method.Parameters[i].Type))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsCancellationToken(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }

        return type.Name == "CancellationToken"
            && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
    }
}
