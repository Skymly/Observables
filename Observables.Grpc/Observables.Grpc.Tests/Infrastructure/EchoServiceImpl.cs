using Grpc.Core;
using Observables.Grpc.Tests.Protos;

namespace Observables.Grpc.Tests.Infrastructure;

public sealed class EchoServiceImpl : Echo.EchoBase
{
    public override Task<EchoReply> UnaryEcho(EchoRequest request, ServerCallContext context) =>
        Task.FromResult(new EchoReply { Text = request.Text });

    public override async Task ServerStreamEcho(
        EchoRequest request,
        IServerStreamWriter<EchoReply> responseStream,
        ServerCallContext context)
    {
        for (var i = 0; i < 3; i++)
        {
            await responseStream
                .WriteAsync(new EchoReply { Text = $"{request.Text}-{i}" })
                .ConfigureAwait(false);
        }
    }
}
