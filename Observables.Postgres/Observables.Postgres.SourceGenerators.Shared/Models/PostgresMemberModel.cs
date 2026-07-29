namespace Observables.Postgres.Generators;

internal sealed record PostgresMemberModel(
    string MemberName,
    string ChannelName,
    PostgresBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    bool HasCancellationToken,
    string? PayloadParameterName,
    string? PayloadTypeDisplay);
