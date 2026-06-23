namespace Observables.Nats.Generators;

internal sealed record NatsInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<NatsMemberModel> Members,
    Nullability Nullability);
