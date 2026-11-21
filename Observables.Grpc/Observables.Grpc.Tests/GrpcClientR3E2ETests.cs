using Grpc.Core;
using Observables.Grpc;
using Observables.Grpc.Tests.Contracts;
using Observables.Grpc.Tests.Infrastructure;
using Observables.Grpc.Tests.Protos;
using R3;

namespace Observables.Grpc.Tests;

[Collection(nameof(GrpcTestHostCollection))]
public sealed class GrpcClientR3E2ETests(GrpcTestHostFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnaryEcho_returns_response()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var reply = await client
            .UnaryEcho(new EchoRequest { Text = "hello-grpc" }, cts.Token)
            .FirstAsync(cts.Token);

        Assert.Equal("hello-grpc", reply.Text);
    }

    [Fact]
    public async Task ServerStreamEcho_emits_multiple_items()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var replies = await client
            .ServerStreamEcho(new EchoRequest { Text = "stream" }, cts.Token)
            .Take(3)
            .ToArrayAsync(cts.Token);

        Assert.Equal(3, replies.Length);
        Assert.Equal("stream-0", replies[0].Text);
        Assert.Equal("stream-2", replies[2].Text);
    }

    [Fact]
    public async Task ClientStreamEcho_sends_multiple_requests()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var requests = Observable.Create<EchoRequest>(observer =>
        {
            observer.OnNext(new EchoRequest { Text = "a" });
            observer.OnNext(new EchoRequest { Text = "b" });
            observer.OnNext(new EchoRequest { Text = "c" });
            observer.OnCompleted();
            return Disposable.Empty;
        });

        var reply = await client.ClientStreamEcho(requests, cts.Token).FirstAsync(cts.Token);
        Assert.Equal("a,b,c", reply.Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplexEcho_releases_the_open_call_on_cancellation(bool cancelCaller)
    {
        var serverFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = await GrpcTestHost.StartAsync(new EchoServiceImpl(() => serverFinished.TrySetResult()));
        using var channel = GrpcTestChannel.Create(host);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        var testToken = TestContext.Current.CancellationToken;
        using var caller = new CancellationTokenSource();
        var firstReply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestsDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Observable.Create<EchoRequest>(observer =>
        {
            observer.OnNext(new EchoRequest { Text = "one" });
            return Disposable.Create(() => requestsDisposed.TrySetResult());
        });

        using var subscription = client.DuplexEcho(requests, caller.Token).Subscribe(
            reply => firstReply.TrySetResult(reply.Text),
            result => completed.TrySetResult(result));

        Assert.Equal("one-ack", await firstReply.Task.WaitAsync(DefaultTimeout, testToken));
        Assert.False(serverFinished.Task.IsCompleted);

        if (cancelCaller)
        {
            await caller.CancelAsync();
        }
        else
        {
            subscription.Dispose();
        }

        await serverFinished.Task.WaitAsync(DefaultTimeout, testToken);
        await requestsDisposed.Task.WaitAsync(DefaultTimeout, testToken);

        if (cancelCaller)
        {
            var result = await completed.Task.WaitAsync(DefaultTimeout, testToken);
            Assert.True(result.IsFailure);
            Assert.True(result.Exception is OperationCanceledException or RpcException { StatusCode: StatusCode.Cancelled });
        }
        else
        {
            Assert.False(caller.IsCancellationRequested);
            Assert.False(completed.Task.IsCompleted);
        }
    }

    [Fact]
    public async Task DuplexEcho_reads_while_client_is_still_writing()
    {
        using var channel = GrpcTestChannel.Create(fixture.Host);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var requests = Observable.Create<EchoRequest>(observer =>
        {
            observer.OnNext(new EchoRequest { Text = "one" });
            observer.OnNext(new EchoRequest { Text = "two" });
            observer.OnNext(new EchoRequest { Text = "three" });
            observer.OnCompleted();
            return Disposable.Empty;
        });

        var replies = await client
            .DuplexEcho(requests, cts.Token)
            .Take(3)
            .ToArrayAsync(cts.Token);

        Assert.Equal(["one-ack", "two-ack", "three-ack"], replies.Select(r => r.Text).ToArray());
    }
}
