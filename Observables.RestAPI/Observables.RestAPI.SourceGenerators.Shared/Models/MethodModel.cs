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
    RestApiMethodSpecModel Spec,
    int? CancellationTokenIndex = null,
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
