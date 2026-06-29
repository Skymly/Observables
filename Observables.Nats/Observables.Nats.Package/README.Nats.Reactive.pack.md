# Observables.Nats.Reactive

Declarative NATS subject proxies with Roslyn source generators — annotate interfaces with `[NatsSubscribe]`/`[NatsPublish]`/`[NatsRequest]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for NATS core realtime messaging.

## Install

```xml
<PackageReference Include="Observables.Nats.Reactive" Version="0.1.2" />
<PackageReference Include="NATS.Client.Core" Version="2.8.1" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same `[Nats]` attributes as the R3 package; use `IObservable<T>` return types and `NatsService.For<T>(connection)`.

## Diagnostics

`OBS9001`–`OBS9007` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
