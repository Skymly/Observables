namespace Observables.Redis.Generators;

internal sealed record RedisMemberModel(
    string MemberName,
    string ChannelTemplate,
    RedisBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> ChannelParameterNames,
    bool HasCancellationToken,
    string? PayloadParameterName,
    string? PayloadTypeDisplay);
