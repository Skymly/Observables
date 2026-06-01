# Observables.RoutedEvents.R3

**Status:** skeleton — generator not implemented yet.

Planned Roslyn source generator for WPF-style **routed events** → R3 `Observable<T>`, migrated from `MvvmAIO.R3.SourceGenerators` (`ObservableEventsGenerator.RoutedDetection`, `AttachedRouted`, etc.).

## Scope (planned)

- Attached / bubbling routed events on dependency objects
- Separate from classic events in `Observables.Events.R3.SourceGenerators`

## References

- `C:\Code\Skymly\MvvmAIO.R3.SourceGenerators\MvvmAIO.R3.SourceGenerators\ObservableEventsGenerator.RoutedDetection.cs`
- `Observables.Events.R3.SourceGenerators` (classic events, shipped)

## Next steps

1. Extract shared routed-event discovery from MVVMAIO into `Observables.RoutedEvents` runtime (if needed)
2. Implement `Observables.RoutedEvents.R3` incremental generator + tests
3. Add `Observables.RoutedEvents.Reactive` parity and `Observables.RoutedEvents.Package`
