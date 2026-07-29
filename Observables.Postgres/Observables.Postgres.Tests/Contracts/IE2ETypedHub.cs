using Observables.Postgres;
using R3;

namespace Observables.Postgres.Tests.Contracts;

[Postgres]
public interface IE2ETypedHub
{
    [Listen("e2e_order")]
    Observable<OrderPayload> Orders { get; }

    [Notify("e2e_order")]
    Observable<Unit> PublishOrder(OrderPayload payload);
}
