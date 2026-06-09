using Observables.Grpc;
using Observables.Grpc.Tests.Protos;
using R3;

namespace Observables.Grpc.Tests.Contracts;

[Grpc("echo.Echo")]
public interface IE2EHub
{
    [GrpcUnary("UnaryEcho")]
    Observable<EchoReply> UnaryEcho(EchoRequest request, CancellationToken cancellationToken = default);

    [GrpcServerStream("ServerStreamEcho")]
    Observable<EchoReply> ServerStreamEcho(EchoRequest request, CancellationToken cancellationToken = default);
}
