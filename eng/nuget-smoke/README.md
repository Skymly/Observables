# NuGet consumer smoke projects

Minimal console apps that reference **only** published or locally packed Observables packages (no sibling project references).

## Local feed (after pack)

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmoke --configuration Release
```

Uses `nuget.config.local` with `packageSourceMapping` (`Observables.*` → local feed only) via `RestoreConfigFile` / `--configfile`. Local packages are packed as `{PackageVersion}-ci.{gitSha}` so they cannot collide with nuget.org `0.2.2`.

## Published feed (nuget.org)

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmokePublished --configuration Release
```

Uses the default NuGet.org source. `ObservablesConsumerPackageVersion` is **required** (Nuke injects `PackageVersion` from `eng/Observables.Package.props`; a manual build must pass `-p:ObservablesConsumerPackageVersion=...`).
