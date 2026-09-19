using Npgsql;
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
    public static void Main()
    {
        using NpgsqlConnection connection = new("Host=127.0.0.1;Port=1;Database=smoke;Username=smoke;Password=smoke");
        ISmokeChannels hub = PostgresService.For<ISmokeChannels>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Postgres Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.Postgres.Reactive consumer smoke OK");
    }
}
