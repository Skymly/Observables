using Observables.Postgres;

namespace Observables.NuGetSmoke.Postgres.Reactive;

[Postgres]
public interface ISmokeChannels
{
    [Listen("ping")]
    IObservable<string> Ping { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Postgres.Reactive consumer smoke OK");
}
