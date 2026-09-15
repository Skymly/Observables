using Observables.Nats;

namespace Observables.Nats.Reactive.Tests.Contracts;

/// <summary>POCO payload boundary; <see cref="IE2EHubReactive"/> only carries primitives.</summary>
[Nats]
public interface IE2EPocoHubReactive
{
    [NatsSubscribe("e2e.poco.reactive")]
    IObservable<Reading> Readings { get; }

    [NatsPublish("e2e.poco.reactive")]
    IObservable<System.Reactive.Unit> PublishReading(Reading reading);
}

public sealed class Reading
{
    public string DeviceId { get; set; } = string.Empty;

    public int Celsius { get; set; }
}
