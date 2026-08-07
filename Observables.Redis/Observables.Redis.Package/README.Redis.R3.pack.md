# Observables.Redis.R3

Declarative Redis Pub/Sub proxies with Roslyn source generators — annotate interfaces with `[RedisSubscribe]`/`[RedisPublish]` to generate [R3](https://github.com/Cysharp/R3) `Observable<T>` proxies for classic Redis Pub/Sub.

## Install

```xml
<PackageReference Include="Observables.Redis.R3" Version="0.2.0" />
<PackageReference Include="StackExchange.Redis" Version="2.8.41" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.Redis;
using R3;
using StackExchange.Redis;

[Redis]
public interface INewsHub
{
    [RedisSubscribe("news.alerts")]
    Observable<string> Alerts { get; }

    [RedisPublish("news.{topic}")]
    Observable<Unit> Publish(string topic, string payload);
}

await using var mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var hub = RedisService.For<INewsHub>(mux);
```

## Diagnostics

`OBS11001`–`OBS11008` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
