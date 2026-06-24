namespace Observables.RestAPI.Generators;

internal sealed record ContextGenerationModel(
    ImmutableEquatableArray<InterfaceModel> Interfaces
);
