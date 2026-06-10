namespace Observables.Sse.Generators;

internal sealed record SseMemberModel(
    string MemberName,
    string EventName,
    SseBoundaryKind BoundaryKind,
    string ReturnTypeDisplay,
    string ResultTypeDisplay);
