namespace Observables.RestAPI.Generators;

internal sealed record ContextGenerationModel(
    string RestApiInternalNamespace,
    string PreserveAttributeDisplayName,
    ImmutableEquatableArray<InterfaceModel> Interfaces
);
