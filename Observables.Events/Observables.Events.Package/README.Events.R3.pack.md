# Observables.Events.R3

Roslyn source generators that turn classic .NET events into [R3](https://github.com/Cysharp/R3) `Observable<T>` streams.

## Install

```xml
<PackageReference Include="Observables.Events.R3" Version="0.1.0-preview1" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.Events.R3;

public class ClickSource
{
    public event Action? Click;
}

var stream = new ClickSource().Events().Click;
```

Optional Avalonia/WPF routed events: set `<ObservableRoutedEvents>true</ObservableRoutedEvents>` in your project (see repository docs).

## Diagnostics

`OBS2001`–`OBS2004` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
