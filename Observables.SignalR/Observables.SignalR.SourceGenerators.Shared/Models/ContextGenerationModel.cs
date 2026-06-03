namespace Observables.SignalR.Generators;

internal sealed record ContextGenerationModel(
    ImmutableEquatableArray<HubInterfaceModel> Interfaces);
