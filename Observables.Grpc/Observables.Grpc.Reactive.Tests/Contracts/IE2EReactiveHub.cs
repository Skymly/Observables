using Observables.Grpc;
using Observables.Grpc.Tests.Protos;

namespace Observables.Grpc.Reactive.Tests.Contracts;

[Grpc("echo.Echo")]
public interface IE2EReactiveHub
{
    [GrpcUnary("UnaryEcho")]
    IObservable<EchoReply> UnaryEcho(EchoRequest request, CancellationToken cancellationToken = default);
}
