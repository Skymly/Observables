namespace Observables.Redis.Generators;

internal sealed record RedisInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<RedisMemberModel> Members,
    Nullability Nullability);
