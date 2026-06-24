namespace Observables.WebSocket.Generators;

internal sealed record WebSocketMemberModel(
    string MemberName,
    WebSocketBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> ParameterNames,
    bool HasCancellationToken);
