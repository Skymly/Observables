namespace Observables.Postgres.Generators;

internal sealed record PostgresInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<PostgresMemberModel> Members,
    Nullability Nullability);
