using Observables.Nats;
using R3;

namespace Observables.Nats.Tests.Contracts;

/// <summary>POCO payload boundary; <see cref="IE2EHub"/> only carries primitives.</summary>
[Nats]
public interface IE2EPocoHub
{
    [NatsSubscribe("e2e.poco")]
    Observable<Reading> Readings { get; }

    [NatsPublish("e2e.poco")]
    Observable<Unit> PublishReading(Reading reading);
}

public sealed class Reading
{
    public string DeviceId { get; set; } = string.Empty;

    public int Celsius { get; set; }
}
