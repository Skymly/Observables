using Observables.Postgres;

namespace Observables.Postgres.Reactive.Tests.Contracts;

[Postgres]
public interface IE2ETypedHubReactive
{
    [Listen("e2e_order")]
    IObservable<OrderPayload> Orders { get; }

    [Notify("e2e_order")]
    IObservable<System.Reactive.Unit> PublishOrder(OrderPayload payload);
}
