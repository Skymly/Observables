# Observables.RoutedEvents.R3.SourceGenerators

**Status:** implemented (R3).

Roslyn source generator for WPF / Avalonia **routed events** → R3 `Observable<T>`, migrated from `MvvmAIO.R3.SourceGenerators`.

## Entry points

| API | Description |
|-----|-------------|
| `RoutedEvents()` | Routed CLR events → `Observable<TEventArgs>` (interface + properties) |
| `RoutedEventHandlers()` | Same filter, `Observable<(object? sender, TEventArgs e)>` |
| `AttachedRoutedEvent` | Avalonia attached routed events on a receiver |
| `AttachedRoutedEventHandler` | Attached routed events, handler tuple shape |

Generated code namespace: `Observables.RoutedEvents.R3` (`internal` interfaces and implementations).

## Requirements

- **WPF:** set MSBuild `UseWPF=true` so `build_property.UseWPF` enables WPF `RoutedEvent` detection.
- **Avalonia:** reference Avalonia interactivity types (or use test stubs); CLR `RoutedEvent` fields are detected via metadata.

## Diagnostics

- `OBS4001` — unsupported event delegate for routed observable generation
- `OBS4002` — unsupported delegate for `RoutedEventHandlers`

## Tests

```powershell
dotnet test Observables.RoutedEvents.R3.SourceGenerators.Tests
```

## References

- Classic events: `Observables.Events.R3.SourceGenerators`
- MvvmAIO: `ObservableEventsGenerator.RoutedDetection.cs`, `ObservableEventsGenerator.AttachedRouted.cs`
