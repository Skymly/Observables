# Observables.Mqtt.R3

Declarative MQTT topic proxies with Roslyn source generators — annotate interfaces with `[MqttSubscribe]`/`[MqttPublish]` to generate [R3](https://github.com/Cysharp/R3) `Observable<T>` proxies for MQTTnet realtime messaging.

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
