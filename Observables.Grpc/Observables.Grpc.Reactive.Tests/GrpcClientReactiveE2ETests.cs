using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Observables.Grpc;
using Observables.Grpc.Reactive.Tests.Contracts;
using Observables.Grpc.Tests.Infrastructure;
using Observables.Grpc.Tests.Protos;

namespace Observables.Grpc.Reactive.Tests;

[Collection(nameof(GrpcTestHostCollection))]
public sealed class GrpcClientReactiveE2ETests(GrpcTestHostFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnaryEcho_returns_response()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EReactiveHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var reply = await client
            .UnaryEcho(new EchoRequest { Text = "hello-reactive-grpc" }, cts.Token)
            .Timeout(DefaultTimeout)
            .FirstAsync()
            .ToTask();

        Assert.Equal("hello-reactive-grpc", reply.Text);
    }

    [Fact]
    public async Task ServerStreamEcho_emits_multiple_items()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EReactiveHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var replies = await client
            .ServerStreamEcho(new EchoRequest { Text = "stream" }, cts.Token)
            .Take(3)
            .ToArray()
            .Timeout(DefaultTimeout)
            .ToTask();

        Assert.Equal(3, replies.Length);
        Assert.Equal("stream-0", replies[0].Text);
        Assert.Equal("stream-2", replies[2].Text);
    }

    [Fact]
    public async Task FromServerStreaming_dispose_cancels_the_pump_without_completing()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EReactiveHub>(channel.CreateCallInvoker());
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = client
            .ServerStreamEcho(new EchoRequest { Text = "hang" }, cts.Token)
            .Subscribe(
                value => ready.TrySetResult(value.Text),
                _ => Interlocked.Exchange(ref errored, 1),
                () => Interlocked.Exchange(ref completed, 1));

        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await Task.Delay(200, cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }
}
