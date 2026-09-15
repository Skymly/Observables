using Observables.Redis;
using R3;

namespace Observables.Redis.Tests.Contracts;

/// <summary>Named counterpart of <see cref="IE2EHub"/>; resolved through <c>RedisService.For&lt;T&gt;()</c>.</summary>
[Redis("e2e-named")]
public interface IE2ENamedHub
{
    [RedisSubscribe("e2e.named")]
    Observable<string> Ping { get; }

    [RedisPublish("e2e.named")]
    Observable<Unit> PublishPing(string payload);
}

/// <summary>Carries a name that no test registers a multiplexer for.</summary>
[Redis("e2e-unregistered")]
public interface IE2EUnregisteredHub
{
    [RedisSubscribe("e2e.unregistered")]
    Observable<string> Ping { get; }
}
