using System.Collections.Immutable;

namespace Observables.RestAPI.Generators;

internal sealed record MethodModel(
    string Name,
    string ReturnType,
    string ContainingType,
    string DeclaredMethod,
    ReturnTypeInfo ReturnTypeMetadata,
    ImmutableEquatableArray<ParameterModel> Parameters,
    ImmutableEquatableArray<TypeConstraint> Constraints,
    bool IsExplicitInterface,
    // HTTP semantic fields (Path B compile-time generation)
    string HttpMethod = "",
    ImmutableEquatableArray<PathFragmentModel> PathFragments = default!,
    int? CancellationTokenIndex = null,
    int? BodyParameterIndex = null,
    int BodySerializationMethod = 0,
    bool BodyBuffered = false,
    ImmutableEquatableArray<string> Headers = default!,
    bool IsMultipart = false,
    string MultipartBoundary = "----MyGreatBoundary",
    int QueryUriFormat = 0,
    bool IsApiResponse = false,
    string ReturnResultType = "",
    string DeserializedResultType = ""
);

internal enum ReturnTypeInfo : byte
{
    Return,
    AsyncVoid,
    AsyncResult,
    SyncVoid,
    R3Observable,
    SystemReactiveObservable,
    Unsupported,
}
