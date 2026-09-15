using Observables.Nats;
using R3;

namespace Observables.Nats.Tests.Contracts;

/// <summary>Named counterpart of <see cref="IE2EHub"/>; resolved through <c>NatsService.For&lt;T&gt;()</c>.</summary>
[Nats("e2e-named")]
public interface IE2ENamedHub
{
    [NatsSubscribe("e2e.named")]
    Observable<string> Ping { get; }

    [NatsPublish("e2e.named")]
    Observable<Unit> PublishPing();
}

/// <summary>Carries a name that no test registers a connection for.</summary>
[Nats("e2e-unregistered")]
public interface IE2EUnregisteredHub
{
    [NatsSubscribe("e2e.unregistered")]
    Observable<string> Ping { get; }
}
