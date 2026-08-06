# Contributing to Observables

Thank you for your interest in Observables. This document covers contribution workflow, releases, and maintainer publishing. User guides live in [Observables.Docs](https://github.com/Skymly/Observables.Docs); runnable apps in [Observables.Samples](https://github.com/Skymly/Observables.Samples).

## Contributing

### Before you open a PR

1. Build and test locally (same as CI):

   ```powershell
   dotnet run --project build/_build.csproj -- --target Ci --configuration Release
   ```

2. If you change user-facing behavior, sync documentation in all three places:
   - This repository (`README.md`, `docs/` per [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md))
   - [Observables.Docs](https://github.com/Skymly/Observables.Docs) (English `docs/` and 简体中文 `docs/zh/` together)
   - [Observables.Samples](https://github.com/Skymly/Observables.Samples) when a new domain or sample is involved

3. Follow existing naming and project layout (see [AGENTS.md](./AGENTS.md) for the authoritative structure, diagnostic ID ranges, and engineering standards).

### Documentation workflow

See [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md) for conventions.

| Change | Docs to update |
|--------|----------------|
| New domain / breaking API | ADR + Design Doc + Observables.Docs |
| New diagnostic ID | Design Doc + `AnalyzerReleases.Unshipped.md` + Observables.Docs |
| Non-breaking API (single domain) | Design Doc + Docs if user-visible |
| Bug fix | Docs if user-visible |

| Directory | Purpose |
|-----------|---------|
| [docs/adr/](docs/adr/README.md) | Architecture decisions (immutable) |
| [docs/design/](docs/design/README.md) | Implementation details per domain |

Release notes: update the version table in this file and [docs/ROADMAP.md](docs/ROADMAP.md) — there is no root `CHANGELOG.md` (see ROADMAP C3).

### PR conventions

- **Titles and descriptions**: English.
- **Scope**: Prefer one solution-folder module per PR (Shared, Events, RestAPI, SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, or Solution Items for root props / `eng/` / `build/` / `.github/`).
- **Commits**: English; do not mention AI or agent tools in commit messages.
- **Do not** change `eng/Observables.Package.props` version or create tags unless the task explicitly requests a release.

### AI agents

Maintainer and agent-oriented rules (version guards, module boundaries, Nuke targets) are in [AGENTS.md](./AGENTS.md). Agents should treat that file as authoritative for implementation details.

## Releases and versioning

Stable packages are published to [nuget.org](https://www.nuget.org/profiles/Skymly) and [GitHub Packages](https://github.com/orgs/Skymly/packages). Install the latest stable version from NuGet; see package readmes for backend-specific dependencies (R3 or System.Reactive).

### Release history (stable)

| Version | Summary |
|---------|---------|
| **0.1.0** | First stable release — eight feature domains, 16 packages (`.R3` + `.Reactive` per domain), public API baseline. |
| **0.1.1** | Stable follow-up — localized **zh-Hans** IntelliSense for Reactive packages. |
| **0.1.2** | Maintenance release — Events incremental generator caching, diagnostic descriptor consolidation, ADR-001 primitives backend decision. |
| **0.1.3** | CI hardening — NuGet consumer smoke job in CI; RestAPI reactive constant unified; E2E port allocation fixed; symbol packages enabled; RestAPI.Reactive project reference aligned; Events Reactive generator constant prefixed. |
| **0.1.4** | Symbol package fix — PDB files included in snupkg (DebugType portable + pack PDBs to lib/ and analyzers/). |
| **0.1.5** | Maintenance release — RestAPI OBS3004 fix (path + [Body]/[Query] parameters no longer falsely rejected); incremental generator cache hit tests across all 8 domains (45 tests). |
| **0.1.6-preview1** | Preview release — NuGet package icon added (hexagon purple→magenta gradient + Rx shape, `PackageIcon` wired into all 16 packages). |
| **0.1.6** | Stable release — package icon; C# keyword identifier escaping in six domain source generators; source generator fail-safe with per-domain internal error diagnostics (OBS2005–OBS9008). |
| **0.1.7** | Stable release — ninth domain **Postgres** (LISTEN/NOTIFY): `Observables.Postgres.R3` / `.Reactive` (+2 → **18** packages); OBS10xxx; PackVerify / nuget-smoke / Public API baselines. |
| **0.1.8** | Tag only — Redis release prep on `main`; Publish skipped (`cursor[bot]` not allowlisted). Superseded by **0.1.9**. |
| **0.1.9** | Stable release — tenth domain **Redis** Pub/Sub: `Observables.Redis.R3` / `.Reactive` (+2 → **20** packages); OBS11xxx; PackVerify / nuget-smoke / Public API baselines. |

Preview builds (`0.1.0-preview*`, `0.1.1-preview*`) were published to NuGet with tags only (no GitHub Release). Details and milestone planning: [docs/ROADMAP.md](docs/ROADMAP.md).

### Package set

**nuget.org (`0.1.9`)**: twenty packages — ten domains, each as `Observables.<Feature>.R3` and `Observables.<Feature>.Reactive`.

| Package ID | Domain |
|------------|--------|
| `Observables.Events.R3` / `.Reactive` | .NET events (classic + optional routed) |
| `Observables.RestAPI.R3` / `.Reactive` | Declarative HTTP client |
| `Observables.SignalR.R3` / `.Reactive` | SignalR hub proxy |
| `Observables.Mqtt.R3` / `.Reactive` | MQTT topic proxy |
| `Observables.WebSocket.R3` / `.Reactive` | WebSocket client proxy |
| `Observables.Grpc.R3` / `.Reactive` | gRPC `CallInvoker` proxy |
| `Observables.Sse.R3` / `.Reactive` | Server-Sent Events (`text/event-stream`) |
| `Observables.Nats.R3` / `.Reactive` | Core NATS subject proxy |
| `Observables.Postgres.R3` / `.Reactive` | PostgreSQL LISTEN/NOTIFY channel proxy |
| `Observables.Redis.R3` / `.Reactive` | Classic Redis Pub/Sub channel proxy |

Version source of truth: `eng/Observables.Package.props` (`PackageVersion` / `Version`).

## Maintainer publishing

Publishing is **tag-triggered** (aligned with [MvvmAIO.Markup](https://github.com/MvvmAIO/MvvmAIO.Markup)): CI does **not** publish on PR or ordinary `main` pushes.

| Release type | Git tag (`v*`) | NuGet | GitHub Release |
|--------------|----------------|-------|----------------|
| Preview (e.g. `0.1.0-preview1`) | Yes | Yes | No |
| Stable (no `-preview` suffix) | Yes | Yes | Yes (maintainer-approved) |

### Steps

1. On `main`, set `eng/Observables.Package.props` **`PackageVersion`** to match the intended tag (`v` + version, e.g. `v0.1.1`).
2. Ensure repository secrets: `NUGET_API_KEY`, `GITHUB_TOKEN` (or PAT with `packages:write`).
3. Push an annotated tag:

   ```powershell
   git tag -a v0.1.1 -m "0.1.1"
   git push origin v0.1.1
   ```

4. [`.github/workflows/release.yml`](.github/workflows/release.yml) runs Nuke **`Publish`** on `push` of `v*` tags (`Test` → `PackVerify` → nuget.org + GitHub Packages). Authorized when `github.actor` **or** `github.triggering_actor` is a maintainer (`Skymly` / `wys0610`) — covers maintainer-driven agent tag pushes; unauthorized runs **fail** (not silent skip). Does **not** create a GitHub Release automatically.
5. For stable releases, create a GitHub Release separately if desired. Emergency republish: `workflow_dispatch` with manual `version` (same authorization).

### Local pack and verify

```powershell
dotnet run --project build/_build.csproj -- --target PackVerify --configuration Release
```

Optional manual publish (normally CI handles this):

```powershell
$env:VERSION = '0.1.1'
$env:NUGET_API_KEY = '...'
$env:GITHUB_TOKEN = '...'
dotnet run --project build/_build.csproj -- --target Publish --configuration Release
```

### GitHub Packages feed

Consumers installing from GitHub Packages add a `nuget.config` source:

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="github" value="https://nuget.pkg.github.com/Skymly/index.json" />
</packageSources>
<packageSourceCredentials>
  <github>
    <add key="Username" value="YOUR_GITHUB_USERNAME" />
    <add key="ClearTextPassword" value="YOUR_GITHUB_PAT_WITH_PACKAGES_READ" />
  </github>
</packageSourceCredentials>
```

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (MIT — see [LICENSE](LICENSE)).
