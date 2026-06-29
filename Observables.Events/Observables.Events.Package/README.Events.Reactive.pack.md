# Observables.Events.Reactive

Roslyn source generator bridging classic .NET events to [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>`. Call `.Events()` on any type with event members — declarative reactive programming, no boilerplate.

## Install

```xml
<PackageReference Include="Observables.Events.Reactive" Version="0.1.2" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

```csharp
using Observables.Events.Reactive;

public class ClickSource
{
    public event Action? Click;
}

var stream = new ClickSource().Events().Click;
```

Optional routed events: `<ObservableRoutedEvents>true</ObservableRoutedEvents>`.

## Diagnostics

`OBS2001`–`OBS2004` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
