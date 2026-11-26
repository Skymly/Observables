# Observables

Roslyn incremental source generators bridging event and IO boundaries to reactive APIs (`R3`, `System.Reactive`, and a planned `ReactiveUI.Primitives` backend per [ADR-005](docs/adr/ADR-005-primitives-backend-adopt.md)). Companion repositories: [Observables.Docs](https://github.com/Skymly/Observables.Docs) and [Observables.Samples](https://github.com/Skymly/Observables.Samples). Roadmap and exit criteria live in [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Build & Verify

```powershell
# CI-equivalent Nuke pipeline (Clean -> Restore -> Compile -> UnitTest + TrimPublish):
dotnet run --project build/_build.csproj -- --target Ci --configuration Release

# Targeted domain tests or full pack + consumer smoke verification:
dotnet run --project build/_build.csproj -- --target Test --test-domains mqtt,shared --configuration Release
dotnet run --project build/_build.csproj -- --target CiPack --configuration Release
```

- **SDK & testhost requirement**: Local full `Ci` requires **native .NET 8, .NET 9, and .NET 10 SDKs + testhosts** (`net9.0` E2E tests cannot roll forward from 8/10). [`global.json`](global.json) pins Nuke `build/` to .NET 10.
- Key Nuke targets: `Ci`, `Test` (`--test-domains`), `Pack` (`--pack-domains`), `PackOnly` (`Pack` + `PackVerify`), `CiPack` (`Test` + `PackOnly` + `NuGetConsumerSmoke`), `PreparePublish`, `PushPackages`.

## Architecture & Invariants

- **Two-layer naming**: Solution generator projects must carry `.SourceGenerators` (`Observables.<Feature>.{R3,Reactive}.SourceGenerators`); NuGet package IDs are `Observables.<Feature>.{R3,Reactive}` produced by one Traversal `Observables.<Feature>.Package` per feature. **Folder name = project name = assembly name** across the repository, with each feature under its own `Observables.<Feature>/` folder matching `/Feature/` in [`Observables.slnx`](Observables.slnx).
- **Backend mutual exclusion**: `R3`, `Reactive`, and `Primitives` NuGet packages are mutually exclusive across every shipped `lib/**/*.dll` (enforced with zero exemptions by `PackVerify` via `PackCsprojReader.ForbiddenBackendAssemblies`). Domain runtimes (`Observables.<Feature>`) stay backend-neutral ([ADR-003](docs/adr/ADR-003-backend-neutral-domain-runtime.md)); MQTT transport adapters follow [ADR-004](docs/adr/ADR-004-mqtt-transport-adapters.md).
- **Shared layer**: `Observables.Core` is link-compiled (`Compile Include`) into domain runtimes—never packed or referenced via `ProjectReference` (read [`docs/design/architecture.md`](docs/design/architecture.md) §2 before adding types). `Observables.SourceGenerators.Shared` must not reference `R3` or `System.Reactive`.
- **MSBuild, CPM & manifest**: Manage package versions in [`Directory.Packages.props`](Directory.Packages.props) (`eng/nuget-smoke/` disables CPM intentionally); every `.Package/Directory.Build.props` must chain-import the root [`Directory.Build.props`](Directory.Build.props). Register every pack project, test project, and smoke consumer in [`eng/Observables.BuildManifest.json`](eng/Observables.BuildManifest.json).
- **Single-module PR boundary**: Scope each Issue and PR to one module in [`Observables.slnx`](Observables.slnx) (`Shared`, one domain folder, `Docs`, `Repository`, or `Solution Items`); never mix `Shared` changes with multiple domains in one PR.

## Diagnostics, Testing & Gotchas

- `TreatWarningsAsErrors=true` on non-skip projects; no unannotated repo-wide `NoWarn`. Shipped runtimes, bridges, and MQTT adapters enforce `Microsoft.CodeAnalysis.PublicApiAnalyzers` (see [`docs/design/public-api.md`](docs/design/public-api.md)).
- Diagnostic IDs are partitioned strictly per domain (see [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)); never reuse or cross ranges, register entries in `AnalyzerReleases.Unshipped.md` (do not suppress `RS2008`), and sync `Observables.Docs` (`diagnostics.md`).
- Each domain requires three test layers: generator snapshot tests, in-process runtime/E2E tests, and `eng/nuget-smoke` consumers.
- Never republish over historical nuget.org `0.2.2` Reactive packages.

## Docs & Agent Config

- Domain glossary: [`CONTEXT.md`](CONTEXT.md)
- Maintainer docs: [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md), [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/adr/`](docs/adr/), [`docs/design/`](docs/design/), [`SECURITY_MODEL.md`](SECURITY_MODEL.md), [`CONTRIBUTING.md`](CONTRIBUTING.md) (no `CHANGELOG.md`)
- Issue routing & triage labels: [`docs/agents/`](docs/agents/)

## Repo-Specific Conventions

- Issue, PR, and commit messages: **English** (overrides any older bilingual wording in `CONTRIBUTING.md`); never mention AI/agent tooling in Git-visible content.
- Version SSOT is [`eng/Observables.Package.props`](eng/Observables.Package.props) (`PackageVersion`); [`release.yml`](.github/workflows/release.yml) publishes on `v*` annotated tags via NuGet Trusted Publishing (OIDC) + GitHub Packages for authorized maintainers (`Skymly`, `wys0610`). Preview tags must never create a GitHub Release.
- Do not commit, push, bump versions, tag, or publish unless explicitly requested.
