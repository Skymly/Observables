using Observables.Postgres;

namespace Observables.Postgres.Reactive.Tests.Contracts;

[Postgres]
public interface IE2EHubReactive
{
    [Listen("e2e_ping")]
    IObservable<string> Ping { get; }

    [Notify("e2e_ping")]
    IObservable<System.Reactive.Unit> PublishPing(string payload);
}
