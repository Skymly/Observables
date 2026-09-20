using Observables.Redis;
using Observables.Redis.Reactive;
using Observables.Redis.Tests.Infrastructure;

namespace Observables.Redis.Reactive.Tests;

public sealed class RedisAdapterCreateTests
{
    [Fact]
    public void FromPublish_serializes_payload_when_observable_is_created()
    {
        var hang = HangingRedis.CreateForPublish();
        var cyclic = new CyclicPayload();
        cyclic.Self = cyclic;

        Assert.ThrowsAny<Exception>(() => SystemReactiveRedisAdapter.FromPublish(hang.Multiplexer, "orders", cyclic, TestContext.Current.CancellationToken));
        Assert.Null(hang.LastPublishArgs);
    }

    sealed class CyclicPayload
    {
        public CyclicPayload Self { get; set; } = null!;
    }
}
