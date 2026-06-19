namespace Observables.RestAPI.Generators;

internal sealed record ContextGenerationModel(
    string RestApiInternalNamespace,
    ImmutableEquatableArray<InterfaceModel> Interfaces
);
