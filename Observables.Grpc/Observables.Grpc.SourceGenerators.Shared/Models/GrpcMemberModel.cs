namespace Observables.Grpc.Generators;

internal sealed record GrpcMemberModel(
    string MemberName,
    string RpcName,
    GrpcBoundaryKind BoundaryKind,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    string? RequestTypeDisplay,
    string? StreamRequestTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> ParameterNames,
    bool HasCancellationToken);
