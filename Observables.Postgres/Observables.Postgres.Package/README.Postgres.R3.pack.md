# Observables.Postgres.R3

Declarative PostgreSQL LISTEN/NOTIFY proxies with Roslyn source generators — annotate interfaces with `[Listen]`/`[Notify]` to generate [R3](https://github.com/Cysharp/R3) `Observable<T>` proxies for Postgres notification channels.

## Install

```xml
<PackageReference Include="Observables.Postgres.R3" Version="0.1.7" />
<PackageReference Include="Npgsql" Version="10.0.3" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Npgsql;
using Observables.Postgres;
using R3;

[Postgres]
public interface IOrderHub
{
    [Listen("orders")]
    Observable<string> Orders { get; }

    [Notify("orders")]
    Observable<Unit> PublishOrder(string payload);
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
var hub = PostgresService.For<IOrderHub>(connection);
```

## Diagnostics

`OBS10001`–`OBS10007` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
