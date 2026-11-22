# Public API baseline (M7)

Observables locks the **NuGet `lib/` runtime surface** with [`Microsoft.CodeAnalysis.PublicApiAnalyzers`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers) (`3.3.2`, pinned in [`Directory.Packages.props`](../../Directory.Packages.props)).

## Scope

| Included | Excluded |
|----------|----------|
| 9 domain runtimes (`Observables.<Feature>/Observables.<Feature>/`) | Events packages (DevelopmentDependency; no consumer `lib/`) |
| 8 R3 bridges (`Observables.<Feature>.R3/`) | |
| 9 Reactive bridges (`Observables.<Feature>.Reactive/`) | Source generators, Analyzers, CodeFixes, tests |
| 2 MQTTnet transport adapters (`Observables.Mqtt.MQTTnet4/`, `Observables.Mqtt.MQTTnet5/`) | |
| Attributes, services, adapters shipped in meta-packages | `.Package` traversal/pack projects, `nuget-smoke` |
| | `Observables.RestAPI.HttpClientFactory` (optional; not in 20-pack manifest) |

The 28 projects are identified by their checked-in `PublicAPI/<tfm>/PublicAPI.Shipped.txt` files. List the project directories from the repository root with this command.

```powershell
git ls-files -- ':(glob)**/PublicAPI/*/PublicAPI.Shipped.txt' |
    ForEach-Object { $_ -replace '/PublicAPI/.*', '' } |
    Sort-Object -Unique
```

Enforcement is wired in [`eng/Observables.PublicApi.props`](../../eng/Observables.PublicApi.props) and imported from [`eng/Observables.ProjectDefaults.props`](../../eng/Observables.ProjectDefaults.props) when `_ObsIsDomainRuntimeProject=true`.

Each project keeps:

- `PublicAPI.Shipped.txt` — APIs shipped in the current release (M7 baseline = `0.1.0` surface).
- `PublicAPI.Unshipped.txt` — APIs added since the last release (empty after a freeze).

Both files start with `#nullable enable` so nullable annotations appear in the baseline.

## Multi-targeting

All 28 covered projects target `net8.0`, `net9.0`, and `net10.0`. The 24 projects that also target `netstandard2.0` keep a baseline for that TFM. The three Postgres projects and the MQTTnet 5 adapter target .NET 8 and later.

PublicApiAnalyzers **3.3.x** expects a **complete** public API list **per TFM** (it does not merge a root file with TFM-specific supplements). Each runtime project therefore keeps:

```text
PublicAPI/
  netstandard2.0/PublicAPI.Shipped.txt
  netstandard2.0/PublicAPI.Unshipped.txt
  net8.0/...
  net9.0/...
  net10.0/...
```

Some members exist only on newer TFMs (for example `JsonNatsPayloadSerializer`, RestAPI `HttpVersionPolicy` settings). Those lines appear only in the `net8.0` / `net9.0` / `net10.0` files.

## Regenerating a baseline (maintainers)

1. Ensure packages restore (`dotnet restore`).
2. Run [`eng/scripts/bootstrap-public-api.ps1`](../../eng/scripts/bootstrap-public-api.ps1) from the repository root. It discovers projects and TFMs from the `PublicAPI/` directory trees. Pass `-ProjectRelativePaths` to select a subset.
3. The script seeds the per-TFM baseline files and runs `dotnet format analyzers --diagnostics RS0016` for each project. It moves the resulting API lists into `Shipped` for the baseline freeze.
4. Verify: `dotnet run --project build/_build.csproj -- --target Ci` and `--target CiPack`.

## Day-to-day contributor workflow

1. **New public member** → add a line to `PublicAPI.Unshipped.txt` only (IDE code-fix “Add public API to Unshipped.txt” on RS0016).
2. **Before release** → move all `Unshipped` lines into `Shipped`; leave `Unshipped` with only `#nullable enable`.
3. **Breaking change** (remove/rename public API) → major version bump; delete the line from `Shipped` (RS0037 / RS0026 will guide review).

`RS0026` (optional-parameter overload rules) is suppressed for domain runtimes via `eng/Observables.PublicApi.props` because preview-era bridge methods already use optional `CancellationToken` overloads. Tighten in a future major if desired.

## CI

`TreatWarningsAsErrors=true` on domain runtimes means undeclared public APIs fail `Ci` with RS0016. Public API files are **not** packed into NuGet artifacts.

## Related

- [Roadmap](../ROADMAP.md) M7
- [Agent rules](../../AGENTS.md) under the public API warning policy
