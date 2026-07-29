namespace Observables.Postgres.Tests.Contracts;

public sealed class OrderPayload
{
    public string OrderId { get; init; } = string.Empty;

    public int Quantity { get; init; }
}
