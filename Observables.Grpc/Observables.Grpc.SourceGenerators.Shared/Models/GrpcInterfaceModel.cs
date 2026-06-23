namespace Observables.Grpc.Generators;

internal sealed record GrpcInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    string ServiceName,
    ImmutableEquatableArray<GrpcMemberModel> Members,
    Nullability Nullability);
