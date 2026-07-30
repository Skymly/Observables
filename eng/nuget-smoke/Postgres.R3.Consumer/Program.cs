using Observables.Postgres;
using R3;

namespace Observables.NuGetSmoke.Postgres.R3;

[Postgres]
public interface ISmokeChannels
{
    [Listen("ping")]
    Observable<string> Ping { get; }

    [Notify("ping")]
    Observable<Unit> PublishPing(string payload);
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Postgres.R3 consumer smoke OK");
}
