# Observables.RoutedEvents.Reactive.SourceGenerators

**Status:** implemented (System.Reactive).

Routed events → `IObservable<T>` (parity with `Observables.RoutedEvents.R3.SourceGenerators`).

## Entry points

Same as R3: `RoutedEvents`, `RoutedEventHandlers`, `AttachedRoutedEvent`, `AttachedRoutedEventHandler`.

Generated namespace: `Observables.RoutedEvents.Reactive`.

## Diagnostics

Uses `OBS4001` / `OBS4002` from Shared (`RoutedEventsDiagnosticDescriptors`).

## Tests

```powershell
dotnet test Observables.RoutedEvents.Reactive.SourceGenerators.Tests
```
