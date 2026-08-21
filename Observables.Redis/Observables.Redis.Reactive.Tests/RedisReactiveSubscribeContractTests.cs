using Observables.Redis.Reactive;
using Observables.Redis.Tests.Infrastructure;
using StackExchange.Redis;
using System.Reactive.Linq;

namespace Observables.Redis.Reactive.Tests;

[Collection(nameof(RedisTestServerCollection))]
public sealed class RedisReactiveSubscribeContractTests(RedisTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task FromSubscribe_dispose_cancels_the_pump_without_completing()
    {
        var channel = "rx.dispose." + Guid.NewGuid().ToString("N");
        await AssertDisposeCancelsAsync(
            mux => SystemReactiveRedisAdapter.FromSubscribe<string>(mux, channel),
            RedisChannel.Literal(channel));
    }

    [Fact]
    public async Task FromPatternSubscribe_dispose_cancels_the_pump_without_completing()
    {
        var prefix = "rx.pdispose." + Guid.NewGuid().ToString("N");
        await AssertDisposeCancelsAsync(
            mux => SystemReactiveRedisAdapter.FromPatternSubscribe<string>(mux, prefix + ".*"),
            RedisChannel.Literal(prefix + ".a"));
    }

    async Task AssertDisposeCancelsAsync(
        Func<IConnectionMultiplexer, IObservable<string>> subscribe,
        RedisChannel warmupChannel)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var mux = await fixture.Server.ConnectAsync(cancellation);
        await using var publisherMux = await fixture.Server.ConnectAsync(cancellation);
        var publisher = publisherMux.GetSubscriber();

        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = subscribe(mux).Subscribe(
            value => ready.TrySetResult(value),
            _ => Interlocked.Exchange(ref errored, 1),
            () => Interlocked.Exchange(ref completed, 1));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(DefaultTimeout);
        await RedisE2EHelpers.PublishUntilReceivedAsync(
            async _ =>
            {
                await publisher.PublishAsync(warmupChannel, "ready");
            },
            ready.Task,
            cts.Token);

        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await Task.Delay(200, cancellation);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }
}
