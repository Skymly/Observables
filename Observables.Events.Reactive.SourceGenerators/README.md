# Observables.Events.Reactive.SourceGenerators

Classic .NET events → `System.Reactive` `IObservable<T>` (parity with `Observables.Events.R3.SourceGenerators`).

- **FromEvents** — `Observable.FromEvent` by delegate shape
- **FromEventHandlers** — `EventHandler` / `(object, T)` via `FromEvent` + sender/args tuple

Generated code namespace: `Observables.Events.Reactive`.

Diagnostics: `OBS2001`, `OBS2002` (shared descriptors in `Observables.SourceGenerators.Shared`).

```powershell
dotnet test ../Observables.Events.Reactive.SourceGenerators.Tests
```
