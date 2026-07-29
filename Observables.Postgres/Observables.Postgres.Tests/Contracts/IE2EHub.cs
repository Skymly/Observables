using Observables.Postgres;
using R3;

namespace Observables.Postgres.Tests.Contracts;

[Postgres]
public interface IE2EHub
{
    [Listen("e2e_ping")]
    Observable<string> Ping { get; }

    [Notify("e2e_ping")]
    Observable<Unit> PublishPing(string payload);
}
