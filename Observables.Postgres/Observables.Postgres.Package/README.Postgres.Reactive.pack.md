# Observables.Postgres.Reactive

Declarative PostgreSQL LISTEN/NOTIFY proxies with Roslyn source generators — annotate interfaces with `[Listen]`/`[Notify]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for Postgres notification channels.

## Install

```xml
<PackageReference Include="Observables.Postgres.Reactive" Version="0.1.7" />
<PackageReference Include="Npgsql" Version="10.0.3" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same `[Postgres]` attributes as the R3 package; use `IObservable<T>` return types and `PostgresService.For<T>(connection)`.

## Diagnostics

`OBS10001`–`OBS10007` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
