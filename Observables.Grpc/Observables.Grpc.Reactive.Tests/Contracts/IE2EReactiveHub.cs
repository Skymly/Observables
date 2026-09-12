using Observables.Grpc;
using Observables.Grpc.Tests.Protos;

namespace Observables.Grpc.Reactive.Tests.Contracts;

[Grpc("echo.Echo")]
public interface IE2EReactiveHub
{
    [GrpcUnary("UnaryEcho")]
    IObservable<EchoReply> UnaryEcho(EchoRequest request, CancellationToken cancellationToken = default);

    [GrpcServerStream("ServerStreamEcho")]
    IObservable<EchoReply> ServerStreamEcho(EchoRequest request, CancellationToken cancellationToken = default);

    [GrpcClientStream("ClientStreamEcho")]
    IObservable<EchoReply> ClientStreamEcho(IObservable<EchoRequest> requests, CancellationToken cancellationToken = default);

    [GrpcDuplex("DuplexEcho")]
    IObservable<EchoReply> DuplexEcho(IObservable<EchoRequest> requests, CancellationToken cancellationToken = default);
}
