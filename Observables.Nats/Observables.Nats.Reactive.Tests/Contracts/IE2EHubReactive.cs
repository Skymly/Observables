using Observables.Nats;

namespace Observables.Nats.Reactive.Tests.Contracts;

[Nats]
public interface IE2EHubReactive
{
    [NatsSubscribe("e2e.ping")]
    IObservable<string> Ping { get; }

    [NatsPublish("e2e.ping")]
    IObservable<System.Reactive.Unit> PublishPing();
}
