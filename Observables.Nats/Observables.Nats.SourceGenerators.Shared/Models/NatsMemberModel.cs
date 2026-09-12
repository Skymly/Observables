namespace Observables.Nats.Generators;

internal sealed record NatsMemberModel(
    string MemberName,
    string SubjectTemplate,
    NatsBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> SubjectParameterNames,
    string? CancellationTokenParameterName,
    string? PayloadParameterName,
    string? PayloadTypeDisplay);
