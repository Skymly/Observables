# Observables.Events.Reactive.SourceGenerators

.NET 事件 → `System.Reactive` `IObservable<T>`（与 `Observables.Events.R3.SourceGenerators` 对称）。

- **Events** — `Observable.FromEvent` by delegate shape
- **EventHandlers** — `EventHandler` / `(object, T)` via `FromEvent` + sender/args tuple
- **RoutedEvents** / **RoutedEventHandlers** / **AttachedRoutedEvent*** — 需 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`

Generated code namespace: `Observables.Events.Reactive`.

Diagnostics: `OBS2001`–`OBS2004` (`Observables.SourceGenerators.Shared`).

```powershell
dotnet test ../Observables.Events.Reactive.SourceGenerators.Tests
```
