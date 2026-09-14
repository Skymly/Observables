using Observables.Redis.Reactive;
using Observables.Redis.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Redis.Reactive.Tests;

public sealed class RedisFromPublishCancellationTests
{
    [Fact]
    public async Task FromPublish_cancel_does_not_wait_on_uncancelable_publish()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var hang = HangingRedis.CreateForPublish();
        using var cts = new CancellationTokenSource();

        var consume = SystemReactiveRedisAdapter.FromPublish(hang.Multiplexer, "cancel.publish", cts.Token)
            .FirstAsync()
            .ToTask(cancellation);
        await hang.PublishStarted.WaitAsync(cancellation);
        cts.Cancel();

        var timeout = Task.Delay(TimeSpan.FromSeconds(2), cancellation);
        var completed = await Task.WhenAny(consume, timeout);
        Assert.Same(consume, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consume);

        Assert.False(hang.InFlight.IsCompleted);
        Assert.NotNull(hang.LastPublishArgs);
        Assert.DoesNotContain(hang.LastPublishArgs, arg => arg is CancellationToken);
    }
}
