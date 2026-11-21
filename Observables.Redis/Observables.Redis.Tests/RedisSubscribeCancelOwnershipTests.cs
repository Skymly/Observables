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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var queue = await delayed.Subscribed.WaitAsync(timeout.Token);
            var redisChannel = RedisChannel.Literal(channel);
            Assert.Equal(1, await publisher.PublishAsync(redisChannel, "before").WaitAsync(timeout.Token));
            Assert.Equal("before", (string?)(await queue.ReadAsync(timeout.Token)).Message);

            subscription.Dispose();
            Assert.False(queue.Completion.IsCompleted);
            delayed.Release.TrySetResult();

            await queue.Completion.WaitAsync(timeout.Token);
            Assert.Equal(0, await publisher.PublishAsync(redisChannel, "after").WaitAsync(timeout.Token));
        }
        finally
        {
            delayed.Release.TrySetResult();
        }
    }
}
