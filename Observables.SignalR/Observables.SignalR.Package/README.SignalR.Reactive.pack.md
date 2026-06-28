# Observables.SignalR.Reactive

Declarative SignalR hub client proxies with Roslyn source generators — annotate interfaces with `[HubInvoke]`/`[HubOn]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for realtime ASP.NET Core SignalR.

## Install

```xml
<PackageReference Include="Observables.SignalR.Reactive" Version="0.1.1" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

Same hub attributes as the R3 package; use `IObservable<T>` return types and `HubService.For<T>(connection)`.

## Diagnostics

`OBS4001`–`OBS4006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
