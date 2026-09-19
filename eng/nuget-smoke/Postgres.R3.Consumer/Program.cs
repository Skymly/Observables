using Npgsql;
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
    public static void Main()
    {
        using NpgsqlConnection connection = new("Host=127.0.0.1;Port=1;Database=smoke;Username=smoke;Password=smoke");
        ISmokeChannels hub = PostgresService.For<ISmokeChannels>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Postgres R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Postgres.R3 consumer smoke OK");
    }
}
