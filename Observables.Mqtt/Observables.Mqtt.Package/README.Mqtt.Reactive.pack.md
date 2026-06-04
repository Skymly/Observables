# Observables.Mqtt.Reactive

Declarative mqtt hub client interfaces for [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>`.

## Install

```xml
<PackageReference Include="Observables.Mqtt.Reactive" Version="0.1.0-preview4" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same hub attributes as the R3 package; use `IObservable<T>` return types and `MqttService.For<T>(connection)`.

## Diagnostics

`OBS5001`–`OBS5006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
