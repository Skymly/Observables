using Observables.Postgres;

namespace Observables.Postgres.Reactive.Tests.Contracts;

[Postgres]
public interface IE2ECustomSerializerHubReactive
{
    [Listen("e2e_colon")]
    IObservable<ColonDelimitedPayload> Messages { get; }

    [Notify("e2e_colon")]
    IObservable<System.Reactive.Unit> Publish(ColonDelimitedPayload payload);
}
