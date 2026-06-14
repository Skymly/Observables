using Observables.Nats;
using System.Reactive;

namespace Observables.Nats.Reactive.Tests.Contracts;

[Nats]
public interface IE2EHub
{
    [NatsSubscribe("e2e/ping")]
    IObservable<string> Ping { get; }

    [NatsPublish("e2e/ping")]
    IObservable<Unit> PublishPing();
}
