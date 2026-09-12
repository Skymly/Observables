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
        if (request.Text == "hang")
        {
            await responseStream
                .WriteAsync(new EchoReply { Text = "ready" })
                .ConfigureAwait(false);
            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            return;
        }

        for (var i = 0; i < 3; i++)
        {
            await responseStream
                .WriteAsync(new EchoReply { Text = $"{request.Text}-{i}" })
                .ConfigureAwait(false);
        }
    }

    public override async Task<EchoReply> ClientStreamEcho(
        IAsyncStreamReader<EchoRequest> requestStream,
        ServerCallContext context)
    {
        var parts = new List<string>();
        while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
        {
            parts.Add(requestStream.Current.Text);
        }

        return new EchoReply { Text = string.Join(",", parts) };
    }

    public override async Task DuplexEcho(
        IAsyncStreamReader<EchoRequest> requestStream,
        IServerStreamWriter<EchoReply> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false))
        {
            await responseStream
                .WriteAsync(new EchoReply { Text = requestStream.Current.Text + "-ack" })
                .ConfigureAwait(false);
        }
    }
}
