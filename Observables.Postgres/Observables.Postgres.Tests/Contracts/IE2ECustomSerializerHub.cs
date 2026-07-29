using Observables.Postgres;
using R3;

namespace Observables.Postgres.Tests.Contracts;

[Postgres]
public interface IE2ECustomSerializerHub
{
    [Listen("e2e_colon")]
    Observable<ColonDelimitedPayload> Messages { get; }

    [Notify("e2e_colon")]
    Observable<Unit> Publish(ColonDelimitedPayload payload);
}
