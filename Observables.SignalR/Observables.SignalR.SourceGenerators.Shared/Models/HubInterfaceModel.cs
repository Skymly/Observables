namespace Observables.SignalR.Generators;

internal sealed record HubInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<HubMemberModel> Members,
    Nullability Nullability);
