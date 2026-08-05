using Observables.Redis;
using Observables.Redis.Reactive.Tests.Contracts;
using Observables.Redis.Tests.Infrastructure;
using StackExchange.Redis;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Redis.Reactive.Tests;

[Collection(nameof(RedisTestServerCollection))]
public sealed class RedisClientReactiveE2ETests(RedisTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RedisSubscribe_Ping_receives_message()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        var hub = RedisService.For<IE2EHubReactive>(mux);
        var subscriber = mux.GetSubscriber();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = hub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
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
        var subHub = RedisService.For<IE2EHubReactive>(subMux);
        var pubHub = RedisService.For<IE2EHubReactive>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPing("from-proxy").Timeout(DefaultTimeout).FirstAsync().ToTask(ct);
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
        var hub = RedisService.For<IE2EHubReactive>(mux);
        var subscriber = mux.GetSubscriber();

        var channel = RedisChannel.Literal("e2e.news.sports");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await subscriber.SubscribeAsync(channel, (_, value) =>
        {
            received.TrySetResult(value.ToString());
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        await hub.PublishNews("sports", "goal!", cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        var payload = await received.Task.WaitAsync(cts.Token);
        Assert.Equal("goal!", payload);

        await subscriber.UnsubscribeAsync(channel);
    }

    [Fact]
    public async Task RedisSubscribe_pattern_envelope_roundtrip_via_For()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var subMux = await fixture.Server.ConnectAsync(cancellation);
        await using var pubMux = await fixture.Server.ConnectAsync(cancellation);
        var subHub = RedisService.For<IE2EHubReactive>(subMux);
        var pubHub = RedisService.For<IE2EHubReactive>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.PatternEnvelope.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPattern("sports", "goal!").Timeout(DefaultTimeout).FirstAsync().ToTask(ct);
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
        var subHub = RedisService.For<IE2EHubReactive>(subMux);
        var pubHub = RedisService.For<IE2EHubReactive>(pubMux);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        var receive = subHub.Bytes.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        var payload = new byte[] { 1, 2, 3, 4 };
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishBytes(payload).Timeout(DefaultTimeout).FirstAsync().ToTask(ct);
            },
            receive,
            cts.Token);

        Assert.Equal(payload, await receive);
    }
}
