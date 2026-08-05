# Observables.Redis.Reactive

Declarative Redis Pub/Sub proxies with Roslyn source generators — annotate interfaces with `[RedisSubscribe]`/`[RedisPublish]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for classic Redis Pub/Sub.

## Install

```xml
<PackageReference Include="Observables.Redis.Reactive" Version="0.1.9" />
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same `[Redis]` attributes as the R3 package; use `IObservable<T>` / `IObservable<RedisMessage<T>>` return types and `RedisService.For<T>(multiplexer)`.

## Diagnostics

`OBS11001`–`OBS11008` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
