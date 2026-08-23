using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.RestAPI.Generators;

/// <summary>
/// RestAPI return-type classification for HTTP method generation.
/// Backend (R3 vs System.Reactive) selection uses <see cref="BackendTokens.IsR3"/>.
/// </summary>
internal static class RestApiReturnTypeClassifier
{
    internal readonly record struct RestApiReturnClassification(
        ReturnTypeInfo Info,
        string ReturnResultType,
        string DeserializedResultType,
        bool IsApiResponse);

    internal static RestApiReturnClassification Classify(
        ITypeSymbol returnType,
        IMethodSymbol methodSymbol,
        WellKnownTypes wellKnownTypes,
        List<Diagnostic> diagnostics)
    {
        var info = ClassifyKind(returnType, methodSymbol, wellKnownTypes, diagnostics);
        var (returnResultType, deserializedResultType, isApiResponse) = Extract(returnType, info, wellKnownTypes);
        return new RestApiReturnClassification(info, returnResultType, deserializedResultType, isApiResponse);
    }

    static ReturnTypeInfo ClassifyKind(
        ITypeSymbol returnType,
        IMethodSymbol methodSymbol,
        WellKnownTypes wellKnownTypes,
        List<Diagnostic> diagnostics)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return ReturnTypeInfo.SyncVoid;
        }

        if (returnType.MetadataName == "Task")
        {
            return ReturnTypeInfo.AsyncVoid;
        }

        if (returnType is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return ReturnTypeInfo.Return;
        }

        var def = namedType.OriginalDefinition;
        var metadata = def.MetadataName;
        if (metadata is "Task`1" or "ValueTask`1")
        {
            return ReturnTypeInfo.AsyncResult;
        }

        var isR3 = BackendTokens.IsR3;
        if (metadata == "Observable`1" && def.ContainingNamespace?.ToDisplayString() == "R3")
        {
            if (isR3)
            {
                return ReturnTypeInfo.R3Observable;
            }

            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedReturnType,
                methodSymbol.Locations.FirstOrDefault(),
                returnType.ToDisplayString()));
            return ReturnTypeInfo.Unsupported;
        }

        if (metadata == "IObservable`1")
        {
            if (isR3)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.SystemReactiveNotReferenced,
                    methodSymbol.Locations.FirstOrDefault(),
                    returnType.ToDisplayString()));
                return ReturnTypeInfo.Unsupported;
            }

            if (wellKnownTypes.TryGet("Observables.RestAPI.Reactive.SystemReactiveObservableAdapter") != null)
            {
                return ReturnTypeInfo.SystemReactiveObservable;
            }

            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.SystemReactiveNotReferenced,
                methodSymbol.Locations.FirstOrDefault(),
                returnType.ToDisplayString()));
            return ReturnTypeInfo.Unsupported;
        }

        return ReturnTypeInfo.Return;
    }

    static (string ReturnResultType, string DeserializedResultType, bool IsApiResponse) Extract(
        ITypeSymbol returnType,
        ReturnTypeInfo returnTypeInfo,
        WellKnownTypes wellKnownTypes)
    {
        if (returnTypeInfo is ReturnTypeInfo.SyncVoid or ReturnTypeInfo.AsyncVoid)
        {
            return ("void", "void", false);
        }

        ITypeSymbol? innerType = null;
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            innerType = namedType.TypeArguments.FirstOrDefault();
        }

        if (innerType is null)
        {
            var display = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (display, display, false);
        }

        var innerDisplay = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isApiResponse = innerType is INamedTypeSymbol innerNamed
            && IsRestApiResponseWrapper(innerNamed, wellKnownTypes);

        if (isApiResponse && innerType is INamedTypeSymbol apiResponseNamed)
        {
            var bodyType = apiResponseNamed.TypeArguments.FirstOrDefault();
            var bodyDisplay = bodyType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
            return (innerDisplay, bodyDisplay, true);
        }

        return (innerDisplay, innerDisplay, false);
    }

    static bool IsRestApiResponseWrapper(INamedTypeSymbol type, WellKnownTypes wellKnownTypes)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.OriginalDefinition;
        var apiResponse = wellKnownTypes.TryGet("Observables.RestAPI.IApiResponse`1");
        var apiResponseImpl = wellKnownTypes.TryGet("Observables.RestAPI.ApiResponse`1");
        return (apiResponse is not null && SymbolEqualityComparer.Default.Equals(definition, apiResponse))
            || (apiResponseImpl is not null && SymbolEqualityComparer.Default.Equals(definition, apiResponseImpl));
    }
}
