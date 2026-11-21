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

        if (returnType.MetadataName is "Task" or "ValueTask"
            && returnType.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
        {
            return ReturnTypeInfo.AsyncVoid;
        }

        if (returnType is not INamedTypeSymbol { IsGenericType: true } namedType)
        {
            return ReturnTypeInfo.Return;
        }

        var def = namedType.OriginalDefinition;
        var metadata = def.MetadataName;
        if (metadata is "Task`1" or "ValueTask`1"
            && def.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
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

        if (metadata == "IObservable`1" && def.ContainingNamespace?.ToDisplayString() == "System")
        {
            if (isR3)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedReturnType,
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

        var resultType = returnTypeInfo switch
        {
            ReturnTypeInfo.AsyncResult or ReturnTypeInfo.R3Observable or ReturnTypeInfo.SystemReactiveObservable =>
                ((INamedTypeSymbol)returnType).TypeArguments[0],
            _ => returnType,
        };

        var resultDisplay = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var responseWithoutBody = wellKnownTypes.TryGet("Observables.RestAPI.IApiResponse");
        if (responseWithoutBody is not null && SymbolEqualityComparer.Default.Equals(resultType, responseWithoutBody))
        {
            return (resultDisplay, "object", true);
        }

        if (resultType is INamedTypeSymbol apiResponse && IsRestApiResponseWrapper(apiResponse, wellKnownTypes))
        {
            var bodyDisplay = apiResponse.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (resultDisplay, bodyDisplay, true);
        }

        return (resultDisplay, resultDisplay, false);
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
