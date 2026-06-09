namespace Observables.Grpc.Generators;

internal enum GrpcBoundaryKind : byte
{
    Unary,
    ServerStream,
    ClientStream,
    Duplex,
}
