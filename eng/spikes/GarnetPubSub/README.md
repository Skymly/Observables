# Garnet Pub/Sub spike (#170)

Runnable probe for the Observables.Redis E2E server decision.

## What it proves

Against an **in-process** `Microsoft.Garnet` `GarnetServer`, using `StackExchange.Redis`:

| Command family | Client API |
|----------------|------------|
| Exact subscribe | `SUBSCRIBE` via `ISubscriber.SubscribeAsync(RedisChannel.Literal(...))` |
| Pattern subscribe | `PSUBSCRIBE` via `ISubscriber.SubscribeAsync(RedisChannel.Pattern(...))` |
| Publish | `PUBLISH` via `ISubscriber.PublishAsync(...)` |

## Run

```powershell
dotnet run --project eng/spikes/GarnetPubSub/GarnetPubSub.Spike.csproj -c Release
```

Exit code `0` = all families passed; non-zero = fail (use a documented fallback server for Redis E2E).

## Constraints

- Spike / tooling only — **not** registered in `eng/Observables.BuildManifest.json`
- Packages are pinned locally (`ManagePackageVersionsCentrally=false`); **do not** add `Microsoft.Garnet` to pack dependency graphs
- No `Observables.Redis` product API surface
