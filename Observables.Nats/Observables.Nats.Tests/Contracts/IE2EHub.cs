using Observables.Nats;
using R3;

namespace Observables.Nats.Tests.Contracts;

[Nats]
public interface IE2EHub
{
    [NatsSubscribe("e2e.ping")]
    Observable<string> Ping { get; }

    [NatsPublish("e2e.ping")]
    Observable<Unit> PublishPing();

    [NatsRequest("e2e.echo")]
    Observable<string> Echo(string message);
}
