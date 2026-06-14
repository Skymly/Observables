# Observables.Nats.R3

Declarative Core NATS subject interfaces for [R3](https://github.com/Cysharp/R3) `Observable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.Nats.R3" Version="0.1.0-preview8" />
<PackageReference Include="NATS.Client.Core" Version="2.8.1" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using NATS.Client.Core;
using Observables.Nats;
using R3;

[Nats]
public interface IOrderHub
{
    [NatsSubscribe("orders.>")]
    Observable<OrderEvent> OrderEvents { get; }

    [NatsPublish("orders.{id}.cancel")]
    Observable<Unit> Cancel(string id);

    [NatsRequest("orders.validate")]
    Observable<ValidationResult> Validate(OrderRequest request);
}

await using var nats = new NatsConnection(new NatsOpts { Url = "nats://127.0.0.1:4222" });
var hub = NatsService.For<IOrderHub>(nats);
```

## Diagnostics

`OBS9001`–`OBS9007` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
