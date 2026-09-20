using Observables.Redis;
using Observables.Redis.Tests.Infrastructure;
using R3;
using StackExchange.Redis;

namespace Observables.Redis.Tests;

[Collection(nameof(RedisTestServerCollection))]
public sealed class RedisSubscribeCancelOwnershipTests(RedisTestServerFixture fixture)
{
    [Fact]
    public async Task FromSubscribe_cancel_of_pending_subscribe_unsubscribes_late_queue()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        await using var publisherMux = await fixture.Server.ConnectAsync(cancellation);
        var channel = "ghost." + Guid.NewGuid().ToString("N");
        var delayed = HangingRedis.CreateDelayedSubscribe(mux);
        var publisher = publisherMux.GetSubscriber();

        using var subscription = RedisObservable.FromSubscribe<string>(delayed.Multiplexer, channel).Subscribe(_ => { });
        await delayed.SubscribeStarted.WaitAsync(cancellation);

        var receiversBefore = await publisher.PublishAsync(RedisChannel.Literal(channel), "before");
        Assert.True(receiversBefore >= 1, $"expected an in-flight subscriber, got {receiversBefore}");

        subscription.Dispose();
        delayed.Release.TrySetResult();

        var receivers = await WaitForPublishReceiversAsync(
            publisher,
            channel,
            expected: 0,
            cancellation);
        Assert.Equal(0, receivers);
    }

    static async Task<long> WaitForPublishReceiversAsync(
        ISubscriber publisher,
        string channel,
        long expected,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        long receivers = -1;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receivers = await publisher.PublishAsync(RedisChannel.Literal(channel), "probe");
            if (receivers == expected)
            {
                return receivers;
            }

            await Task.Delay(50, cancellationToken);
        }

        return receivers;
    }
}
