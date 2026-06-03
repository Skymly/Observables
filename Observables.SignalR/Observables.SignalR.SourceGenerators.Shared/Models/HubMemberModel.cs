namespace Observables.SignalR.Generators;

internal sealed record HubMemberModel(
    string MemberName,
    string HubMethodName,
    HubBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> ParameterNames,
    bool HasCancellationToken);
