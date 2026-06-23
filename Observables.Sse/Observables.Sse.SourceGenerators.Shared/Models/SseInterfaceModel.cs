namespace Observables.Sse.Generators;

internal sealed record SseInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<SseMemberModel> Members,
    Nullability Nullability);
