using Observables.Nats;

namespace Observables.NuGetSmoke.Nats.Reactive;

[Nats]
public interface ISmokeSubjects
{
    [NatsSubscribe("ping")]
    IObservable<string> Ping { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Nats.Reactive consumer smoke OK");
}
