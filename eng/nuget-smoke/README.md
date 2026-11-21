# NuGet consumer smoke projects

Minimal console apps that reference **only** published or locally packed Observables packages (no sibling project references).

## Local feed

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmoke --configuration Release
```

Builds packages as `{PackageVersion}-ci.{gitSha}` and uses `nuget.config.local` to map `Observables.*` to the local feed. Each run restores into a fresh package cache and compares the restored package hashes with the packages it built.

## Published feed (nuget.org)

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmokePublished --configuration Release
```

Uses only nuget.org and verifies the restored packages' source in a fresh package cache. Nuke takes the version from `eng/Observables.Package.props` or `--version`. For a manual build, pass `-p:ObservablesConsumerPackageVersion=...`.

The WPF consumers require routed-event delivery. Published versions that predate the WPF delegate fix, including 0.4.1, fail that assertion.
