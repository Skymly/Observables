# Observables.Mqtt.Reactive

Declarative MQTT topic proxies with Roslyn source generators — annotate interfaces with `[MqttSubscribe]`/`[MqttPublish]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for MQTTnet realtime messaging.

## Install

```xml
<PackageReference Include="Observables.Mqtt.Reactive" Version="0.1.1" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same hub attributes as the R3 package; use `IObservable<T>` return types and `MqttService.For<T>(connection)`.

## Diagnostics

`OBS5001`–`OBS5006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
