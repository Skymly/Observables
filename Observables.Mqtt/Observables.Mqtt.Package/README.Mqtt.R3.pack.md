# Observables.Mqtt.R3

Declarative mqtt hub client interfaces for [R3](https://github.com/Cysharp/R3) `Observable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.Mqtt.R3" Version="0.1.1" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.Mqtt;
using R3;
using MQTTnet;

[Hub]
public interface IChatHub
{
    [HubInvoke]
    Observable<int> GetUserCount();

    [HubOn("ReceiveMessage")]
    Observable<ChatMessage> ReceiveMessage { get; }
}

var hub = MqttService.For<IChatHub>(connection);
```

## Diagnostics

`OBS5001`–`OBS5006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
