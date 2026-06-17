# NuGet consumer smoke projects

Minimal console apps that reference **only** published or locally packed Observables packages (no sibling project references).

## Local feed (after pack)

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmoke --configuration Release
```

Uses `nuget.config.local` pointing at `artifacts/package/`.

## Published feed (nuget.org)

```powershell
dotnet run --project build/_build.csproj -- --target NuGetConsumerSmokePublished --configuration Release
```

Uses the default NuGet.org source and `ObservablesConsumerPackageVersion` (default `0.1.1`).
