# Observables.Events.Reactive

Roslyn source generators that turn classic .NET events into `System.Reactive` `IObservable<T>` streams.

## Install

```xml
<PackageReference Include="Observables.Events.Reactive" Version="0.1.0-preview2" />
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
