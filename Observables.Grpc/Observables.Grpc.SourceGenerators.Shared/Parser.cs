using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Grpc.Generators;

internal static class Parser
{
    static readonly SymbolDisplayFormat DisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static (List<Diagnostic> diagnostics, ContextGenerationModel model) GenerateGrpcStubs(
        CSharpCompilation compilation,
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken)
    {
        var grpcAttribute = compilation.GetTypeByMetadataName("Observables.Grpc.GrpcAttribute");
        var unaryAttribute = compilation.GetTypeByMetadataName("Observables.Grpc.GrpcUnaryAttribute");
        var serverStreamAttribute = compilation.GetTypeByMetadataName("Observables.Grpc.GrpcServerStreamAttribute");
        var clientStreamAttribute = compilation.GetTypeByMetadataName("Observables.Grpc.GrpcClientStreamAttribute");
        var duplexAttribute = compilation.GetTypeByMetadataName("Observables.Grpc.GrpcDuplexAttribute");
        var observableType = compilation.GetTypeByMetadataName(BackendTokens.ObservableMetadataName);

        return IoProxyModelAssembly.Parse<GrpcMemberModel, GrpcInterfaceModel, ContextGenerationModel>(
            markedInterfaces,
            cancellationToken,
            coreReferenced: grpcAttribute is not null,
            coreNotReferenced: DiagnosticDescriptors.GrpcCoreNotReferenced,
            emptyModel: static () => new ContextGenerationModel(ImmutableEquatableArray.Empty<GrpcInterfaceModel>()),
            tryAddMethod: (marked, method, members, diagnostics) => TryAddMethod(
                method,
                marked.InterfaceSymbol,
                compilation,
                unaryAttribute,
                serverStreamAttribute,
                clientStreamAttribute,
                duplexAttribute,
                observableType,
                members,
                diagnostics),
            tryAddProperty: (marked, property, members, diagnostics) =>
            {
                if (property.GetAttributes().Length > 0)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.InvalidGrpcMember,
                            property.Locations.FirstOrDefault(),
                            marked.InterfaceSymbol.Name,
                            property.Name));
                }
            },
            createInterface: static (marked, className, members) => new GrpcInterfaceModel(
                $"{className}.Grpc.g.cs",
                className,
                marked.InterfaceSymbol.ToDisplayString(DisplayFormat),
                BackendTokens.QualifyGeneratedNamespace("Observables.Grpc"),
                GetServiceName(marked.InterfaceSymbol) ?? marked.InterfaceSymbol.Name.TrimStart('I'),
                members,
                marked.Nullability),
            createContext: static interfaces => new ContextGenerationModel(interfaces));
    }

    static void TryAddMethod(
        IMethodSymbol method,
        INamedTypeSymbol ifaceSymbol,
        CSharpCompilation compilation,
        INamedTypeSymbol? unaryAttribute,
        INamedTypeSymbol? serverStreamAttribute,
        INamedTypeSymbol? clientStreamAttribute,
        INamedTypeSymbol? duplexAttribute,
        INamedTypeSymbol? observableType,
        List<GrpcMemberModel> members,
        List<Diagnostic> diagnostics)
    {
        GrpcBoundaryKind? boundary = null;
        string rpcName = method.Name;

        if (unaryAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, unaryAttribute))
        {
            boundary = GrpcBoundaryKind.Unary;
            rpcName = GetRpcName(method, "GrpcUnaryAttribute") ?? method.Name;
        }
        else if (serverStreamAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, serverStreamAttribute))
        {
            boundary = GrpcBoundaryKind.ServerStream;
            rpcName = GetRpcName(method, "GrpcServerStreamAttribute") ?? method.Name;
        }
        else if (clientStreamAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, clientStreamAttribute))
        {
            boundary = GrpcBoundaryKind.ClientStream;
            rpcName = GetRpcName(method, "GrpcClientStreamAttribute") ?? method.Name;
        }
        else if (duplexAttribute is not null && IoProxyInterfaceWalk.HasAttribute(method, duplexAttribute))
        {
            boundary = GrpcBoundaryKind.Duplex;
            rpcName = GetRpcName(method, "GrpcDuplexAttribute") ?? method.Name;
        }

        if (boundary is null)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGrpcMember,
                    method.Locations.FirstOrDefault(),
                    ifaceSymbol.Name,
                    method.Name));
            return;
        }

        if (!ObservableReturnTypeParser.TryParse(
                method.ReturnType,
                compilation,
                "Observables.Grpc.Reactive.SystemReactiveGrpcAdapter",
                observableType,
                unitType: null,
                requiresUnitPayload: false,
                DiagnosticDescriptors.UnsupportedReturnType,
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                method.Locations.FirstOrDefault(),
                diagnostics,
                out var resultType,
                out var returnDisplay))
        {
            return;
        }

        var nonCtParams = method.Parameters.Where(static p => !IsCancellationToken(p.Type)).ToList();
        string? requestType = null;
        string? streamRequestType = null;

        switch (boundary)
        {
            case GrpcBoundaryKind.Unary:
            case GrpcBoundaryKind.ServerStream:
                if (nonCtParams.Count != 1)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnsupportedGrpcOption,
                            method.Locations.FirstOrDefault(),
                            ifaceSymbol.Name,
                            method.Name));
                    return;
                }

                requestType = nonCtParams[0].Type.ToDisplayString(DisplayFormat);
                break;

            case GrpcBoundaryKind.ClientStream:
            case GrpcBoundaryKind.Duplex:
                if (nonCtParams.Count != 1
                    || !TryGetObservableElementType(nonCtParams[0].Type, observableType, out streamRequestType))
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnsupportedGrpcOption,
                            method.Locations.FirstOrDefault(),
                            ifaceSymbol.Name,
                            method.Name));
                    return;
                }

                break;
        }

        var (declarations, names, hasCt) = BuildParameters(method);
        members.Add(
            new GrpcMemberModel(
                IdentifierHelper.Escape(method.Name),
                rpcName,
                boundary.Value,
                returnDisplay,
                resultType,
                requestType,
                streamRequestType,
                declarations.ToImmutableEquatableArray(),
                names.ToImmutableEquatableArray(),
                hasCt));
    }

    static string? GetServiceName(INamedTypeSymbol ifaceSymbol)
    {
        foreach (var attr in ifaceSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "GrpcAttribute")
            {
                continue;
            }

            if (attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }

            return null;
        }

        return null;
    }

    static string? GetRpcName(IMethodSymbol method, string attributeClassName)
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

    static bool TryGetObservableElementType(
        ITypeSymbol type,
        INamedTypeSymbol? observableType,
        out string elementTypeDisplay)
    {
        elementTypeDisplay = string.Empty;
        if (observableType is null
            || type is not INamedTypeSymbol { IsGenericType: true } named
            || named.TypeArguments.Length != 1
            || !SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, observableType))
        {
            return false;
        }

        elementTypeDisplay = named.TypeArguments[0].ToDisplayString(DisplayFormat);
        return true;
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
                    $"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)} = default");
                continue;
            }

            names.Add(IdentifierHelper.Escape(parameter.Name));
            declarations.Add($"{parameter.Type.ToDisplayString(DisplayFormat)} {IdentifierHelper.Escape(parameter.Name)}");
        }

        return (declarations, names, hasCt);
    }

    static bool IsCancellationToken(ITypeSymbol type) =>
        type.Name == "CancellationToken"
        && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
}
