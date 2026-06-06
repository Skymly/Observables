using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Observables.WebSocket.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

#if WEBSOCKET_R3
    const string ObservableMetadataName = "R3.Observable`1";
    const string UnitMetadataName = "R3.Unit";
#else
    const string ObservableMetadataName = "System.IObservable`1";
    const string UnitMetadataName = "System.Reactive.Unit";
#endif

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateWebSocketStubs(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<Diagnostic>();
        var wsAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketAttribute");
        if (wsAttribute is null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.WebSocketCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<WebSocketInterfaceModel>()));
        }

        var sendAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketSendAttribute");
        var receiveAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketReceiveAttribute");
        var connectAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketConnectAttribute");
        var closeAttribute = compilation.GetTypeByMetadataName("Observables.WebSocket.WebSocketCloseAttribute");
        var observableType = compilation.GetTypeByMetadataName(ObservableMetadataName);
        var unitType = compilation.GetTypeByMetadataName(UnitMetadataName);

        var interfaces = new List<WebSocketInterfaceModel>();

        foreach (var group in candidateInterfaces.GroupBy(static i => i.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var ifaceSyntax in group)
            {
                if (semanticModel.GetDeclaredSymbol(ifaceSyntax, cancellationToken) is not INamedTypeSymbol ifaceSymbol)
                {
                    continue;
                }

                if (!HasAttribute(ifaceSymbol, wsAttribute))
                {
                    continue;
                }

                var nullable = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(ifaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = new List<WebSocketMemberModel>();
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
                                sendAttribute,
                                connectAttribute,
                                closeAttribute,
                                observableType,
                                unitType,
                                members,
                                diagnostics);
                            break;
                        case IPropertySymbol property:
                            TryAddProperty(
                                property,
                                ifaceSymbol,
                                receiveAttribute,
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
                    new WebSocketInterfaceModel(
                        $"{className}.WebSocket.g.cs",
                        className,
                        ifaceSymbol.ToDisplayString(DisplayFormat),
#if WEBSOCKET_R3
                        "Observables.WebSocket.Generated",
#else
                        "Observables.WebSocket.Reactive.Generated",
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
        INamedTypeSymbol? sendAttribute,
        INamedTypeSymbol? connectAttribute,
        INamedTypeSymbol? closeAttribute,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        List<WebSocketMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        WebSocketBoundaryKind? boundary = null;
        string messageName = method.Name;

        if (sendAttribute is not null && HasAttribute(method, sendAttribute))
        {
            boundary = WebSocketBoundaryKind.Send;
            messageName = GetMessageName(method, "WebSocketSendAttribute") ?? method.Name;
        }
        else if (connectAttribute is not null && HasAttribute(method, connectAttribute))
        {
            boundary = WebSocketBoundaryKind.Connect;
        }
        else if (closeAttribute is not null && HasAttribute(method, closeAttribute))
        {
            boundary = WebSocketBoundaryKind.Close;
        }

        if (boundary is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidWebSocketMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!TryParseObservableReturn(method.ReturnType, observableType, unitType, boundary.Value, out var resultType, out var returnDisplay))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedReturnType,
                    method.Locations.FirstOrDefault(),
                    method.ReturnType.ToDisplayString(DisplayFormat)));
            return;
        }

        // Connect: must have exactly one Uri parameter
        if (boundary == WebSocketBoundaryKind.Connect)
        {
            var nonCtParams = method.Parameters
                .Where(static p => !IsCancellationToken(p.Type))
                .ToList();
            if (nonCtParams.Count != 1 || nonCtParams[0].Type.ToDisplayString() != "System.Uri")
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedWebSocketOption,
                        method.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        method.Name));
                return;
            }
        }

        // Close: no non-CT parameters allowed
        if (boundary == WebSocketBoundaryKind.Close)
        {
            var nonCtParams = method.Parameters
                .Where(static p => !IsCancellationToken(p.Type))
                .ToList();
            if (nonCtParams.Count != 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedWebSocketOption,
                        method.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        method.Name));
                return;
            }
        }

        var (declarations, names, hasCt) = BuildParameters(method);
        members.Add(
            new WebSocketMemberModel(
                method.Name,
                messageName,
                boundary.Value,
                false,
                returnDisplay,
                resultType,
                declarations.ToImmutableEquatableArray(),
                names.ToImmutableEquatableArray(),
                hasCt));
    }

    static void TryAddProperty(
        IPropertySymbol property,
        INamedTypeSymbol ifaceSymbol,
        INamedTypeSymbol? receiveAttribute,
        INamedTypeSymbol? observableType,
        List<WebSocketMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        if (receiveAttribute is null || !HasAttribute(property, receiveAttribute))
        {
            if (property.GetAttributes().Length > 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InvalidWebSocketMember,
                        property.Locations.FirstOrDefault(),
                        ifaceSymbol.Name,
                        property.Name));
            }

            return;
        }

        var messageName = GetMessageNameFromProperty(property) ?? property.Name;

        if (!TryParseObservableReturn(property.Type, observableType, unitType: null, WebSocketBoundaryKind.Receive, out var resultType, out var returnDisplay))
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedReturnType,
                    property.Locations.FirstOrDefault(),
                    property.Type.ToDisplayString(DisplayFormat)));
            return;
        }

        members.Add(
            new WebSocketMemberModel(
                property.Name,
                messageName,
                WebSocketBoundaryKind.Receive,
                true,
                returnDisplay,
                resultType,
                ImmutableEquatableArray.Empty<string>(),
                ImmutableEquatableArray.Empty<string>(),
                false));
    }

    static bool TryParseObservableReturn(
        ITypeSymbol returnType,
        INamedTypeSymbol? observableType,
        INamedTypeSymbol? unitType,
        WebSocketBoundaryKind boundary,
        out string resultTypeDisplay,
        out string returnTypeDisplay)
    {
        resultTypeDisplay = string.Empty;
        returnTypeDisplay = returnType.ToDisplayString(DisplayFormat);

        if (observableType is null || returnType is not INamedTypeSymbol named
            || !SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, observableType))
        {
            return false;
        }

        if (named.TypeArguments.Length != 1)
        {
            return false;
        }

        resultTypeDisplay = named.TypeArguments[0].ToDisplayString(DisplayFormat);

        if (boundary == WebSocketBoundaryKind.Send
            || boundary == WebSocketBoundaryKind.Connect
            || boundary == WebSocketBoundaryKind.Close)
        {
            if (unitType is null
                || !SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], unitType))
            {
                return false;
            }
        }

        return true;
    }

    static string? GetMessageName(IMethodSymbol method, string attributeClassName)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (attr.AttributeClass?.Name == attributeClassName)
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

    static string? GetMessageNameFromProperty(IPropertySymbol property)
    {
        foreach (var attr in property.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "WebSocketReceiveAttribute")
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

    static (List<string> declarations, List<string> names, bool hasCancellationToken) BuildParameters(
        IMethodSymbol method)
    {
        var declarations = new List<string>();
        var names = new List<string>();
        var hasCt = false;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var parameter = method.Parameters[i];
            if (i == method.Parameters.Length - 1 && IsCancellationToken(parameter.Type))
            {
                hasCt = true;
                declarations.Add(
                    $"{parameter.Type.ToDisplayString(DisplayFormat)} {parameter.Name} = default");
                continue;
            }

            names.Add(parameter.Name);
            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {parameter.Name}");
        }

        return (declarations, names, hasCt);
    }

    static bool IsCancellationToken(ITypeSymbol type) =>
        type.Name == "CancellationToken"
        && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
}
