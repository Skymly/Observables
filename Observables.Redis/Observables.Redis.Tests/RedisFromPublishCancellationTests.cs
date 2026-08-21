using Observables.Redis.Tests.Infrastructure;
using R3;

namespace Observables.Redis.Tests;

public sealed class RedisFromPublishCancellationTests
{
    [Fact]
    public async Task FromPublish_cancel_does_not_wait_on_uncancelable_publish()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var (multiplexer, publishStarted) = HangingRedis.CreateForPublish();
        using var cts = new CancellationTokenSource();

        var consume = RedisObservable.FromPublish(multiplexer, "cancel.publish", cts.Token).FirstAsync(cancellation);
        await publishStarted.WaitAsync(cancellation);
        cts.Cancel();

        var timeout = Task.Delay(TimeSpan.FromSeconds(2), cancellation);
        var completed = await Task.WhenAny(consume, timeout);
        Assert.Same(consume, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consume);
    }
}
