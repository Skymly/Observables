namespace Observables.WebSocket.Generators;

internal sealed record WebSocketInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<WebSocketMemberModel> Members,
    Nullability Nullability);
