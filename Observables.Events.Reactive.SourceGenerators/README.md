# Observables.Events.Reactive.SourceGenerators

Classic .NET events → `System.Reactive` `IObservable<T>` (parity with `Observables.Events.R3.SourceGenerators`).

- **Events** — `Observable.FromEvent` by delegate shape
- **EventHandlers** — `EventHandler` / `(object, T)` via `FromEvent` + sender/args tuple

Generated code namespace: `Observables.Events.Reactive`.

Diagnostics: `OBS2001`, `OBS2002` (shared descriptors in `Observables.SourceGenerators.Shared`).

```powershell
dotnet test ../Observables.Events.Reactive.SourceGenerators.Tests
```
