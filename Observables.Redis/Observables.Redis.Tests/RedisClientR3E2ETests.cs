using Observables.Redis;
using Observables.Redis.Tests.Contracts;
using Observables.Redis.Tests.Infrastructure;
using R3;
using StackExchange.Redis;

namespace Observables.Redis.Tests;

[Collection(nameof(RedisTestServerCollection))]
public sealed class RedisClientR3E2ETests(RedisTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RedisSubscribe_Ping_receives_message()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        var hub = RedisService.For<IE2EHub>(mux);
        var subscriber = mux.GetSubscriber();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await subscriber.PublishAsync(RedisChannel.Literal("e2e.ping"), "hello");
            },
            receive,
            cts.Token);

        Assert.Equal("hello", await receive);
    }

    [Fact]
    public async Task RedisPublish_PublishPing_reaches_subscriber()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var subMux = await fixture.Server.ConnectAsync(cancellation);
        await using var pubMux = await fixture.Server.ConnectAsync(cancellation);
        var subHub = RedisService.For<IE2EHub>(subMux);
        var pubHub = RedisService.For<IE2EHub>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.Ping.FirstAsync(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPing("from-proxy").FirstAsync(ct);
            },
            receive,
            cts.Token);

        Assert.Equal("from-proxy", await receive);
    }

    [Fact]
    public async Task RedisPublish_template_param_roundtrip()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        var hub = RedisService.For<IE2EHub>(mux);
        var subscriber = mux.GetSubscriber();

        var channel = RedisChannel.Literal("e2e.news.sports");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(channel, (_, value) =>
        {
            received.TrySetResult(value.ToString());
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        await hub.PublishNews("sports", "goal!", cts.Token).FirstAsync(cts.Token);
        var payload = await received.Task.WaitAsync(cts.Token);
        Assert.Equal("goal!", payload);

        await subscriber.UnsubscribeAsync(channel);
    }

    [Fact]
    public async Task RedisSubscribe_dispose_unsubscribes()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        var hub = RedisService.For<IE2EHub>(mux);
        var publisher = mux.GetSubscriber();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;

        var subscription = hub.Ping.Subscribe(value =>
        {
            var n = Interlocked.Increment(ref count);
            if (n == 1)
            {
                first.TrySetResult(value);
            }
            else
            {
                second.TrySetResult(value);
            }
        });

        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async _ =>
            {
                await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "one");
            },
            first.Task,
            cts.Token);

        Assert.Equal("one", await first.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await Task.Delay(200, cts.Token);

        await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "two");
        var completed = await Task.WhenAny(second.Task, Task.Delay(500, cts.Token));
        Assert.NotSame(second.Task, completed);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact]
    public async Task RedisSubscribe_delivery_is_sequential()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        var hub = RedisService.For<IE2EHub>(mux);
        var publisher = mux.GetSubscriber();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var ordered = new List<string>();
        var gate = new object();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var _ = hub.Ping.Subscribe(value =>
        {
            if (value == "__warm__")
            {
                return;
            }

            lock (gate)
            {
                ordered.Add(value);
                if (ordered.Count >= 3)
                {
                    done.TrySetResult();
                }
            }
        });

        using (var warmCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
        {
            warmCts.CancelAfter(TimeSpan.FromSeconds(5));
            var warm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var warmSub = hub.Ping.Subscribe(value =>
            {
                if (value == "__warm__")
                {
                    warm.TrySetResult();
                }
            });
            await RedisE2EHelpers.PublishUntilReceivedAsync(
                async __ =>
                {
                    await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "__warm__");
                },
                warm.Task,
                warmCts.Token);
            await warm.Task;
        }

        await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "a");
        await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "b");
        await publisher.PublishAsync(RedisChannel.Literal("e2e.ping"), "c");

        await done.Task.WaitAsync(cts.Token);
        Assert.Equal(["a", "b", "c"], ordered);
    }

    [Fact]
    public async Task RedisSubscribe_pattern_envelope_roundtrip_via_For()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var subMux = await fixture.Server.ConnectAsync(cancellation);
        await using var pubMux = await fixture.Server.ConnectAsync(cancellation);
        var subHub = RedisService.For<IE2EHub>(subMux);
        var pubHub = RedisService.For<IE2EHub>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.PatternEnvelope.FirstAsync(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPattern("sports", "goal!").FirstAsync(ct);
            },
            receive,
            cts.Token);

        var message = await receive;
        Assert.Equal("e2e.pattern.sports", message.Channel);
        Assert.Equal("goal!", message.Payload);
    }

    [Fact]
    public async Task RedisPublish_byte_array_raw_roundtrip()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var subMux = await fixture.Server.ConnectAsync(cancellation);
        await using var pubMux = await fixture.Server.ConnectAsync(cancellation);
        var subHub = RedisService.For<IE2EHub>(subMux);
        var pubHub = RedisService.For<IE2EHub>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.Bytes.FirstAsync(cts.Token);
        var payload = new byte[] { 1, 2, 3, 4 };
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishBytes(payload).FirstAsync(ct);
            },
            receive,
            cts.Token);

        Assert.Equal(payload, await receive);
    }
}
