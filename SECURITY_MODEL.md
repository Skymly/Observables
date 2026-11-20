# Observables security model

## Scope and trust boundaries

Skymly/Observables is an open-source .NET library and Roslyn generator suite, not a hosted service or account provider. NuGet packages ship analyzers/generators that run in consumers' compiler or IDE processes, plus generated proxies and runtime libraries that execute inside the consumer application. Runtime clients receive input from servers and brokers; endpoint selection, credentials, transport configuration, and service authorization generally come from the consuming application or the protocol SDK. This repository also contains the privileged GitHub Actions pipeline that builds and publishes packages, and syncs companion repositories. See [README](README.md), [architecture](docs/design/architecture.md), and [contribution and release guide](CONTRIBUTING.md).

| Component | Entry points, trust boundary, and assets |
| --- | --- |
| **Compiler Tooling** | Events generators, Feature `*.SourceGenerators*` projects, and `Observables.Shared` compiler-side projects read consumer C# symbols, attributes and build options and emit source into consumer assemblies. Compiler/IDE availability and generated code integrity matter. Project authors and inputs supplied by dependencies can influence these inputs; network peers cannot reach these paths directly. Key references: `ProxyClassEmitter.cs`, `ProxyRegistrationEmitter.cs`, and the per-Feature emitters. |
| **REST Client** | `Observables.RestAPI` runtime, adapters and HttpClientFactory extension. Caller input and scoped request options flow through `RestService`, `RestApiCallScope` and `RestApiBridge` into HTTP requests. `AuthenticatedHttpClientHandler` supplies authorization headers; remote servers control replies. Protect tokens and headers, request destinations, bodies and responses, and separation between concurrent requests. |
| **Streaming Clients** | Runtime/adapters in `Observables.SignalR`, `Observables.WebSocket`, `Observables.Grpc`, and `Observables.Sse`. Consumer-provided connections or channel configuration meet data from hubs, servers and peers through the Feature protocol bridges. Examples: `WebSocketReceivePump.cs` and `SseProtocol.cs`. Protect stream content and the host's CPU, memory, and connection capacity. |
| **Messaging Clients** | Runtime/adapters in `Observables.Mqtt`, `Observables.Nats`, `Observables.Postgres`, and `Observables.Redis`. Broker/database clients and app-supplied topic/subject/channel values determine destinations; protocol bridges receive and deserialize remote messages. Anyone able to publish to a matched destination, or control the broker/server, can influence the delivered data. Protect connection credentials, payloads, correct subscription routing, and app availability. |
| **Local Events and Shared Runtime** | Events runtime/build surfaces and `Observables.Shared/Observables.Core` cover local event adapters and reusable registration/types (including `GeneratedProxyFactoryRegistry.cs`). App code and its dependencies can raise events, manage subscriptions or register factories in-process. Protect subscriber lifetime and proxy selection; this is not an isolation boundary against code already executing in the host. |
| **Package Release** | `.github/workflows/{ci,release}.yml`, `build/`, `eng/`, root build props, dependency versions, and Feature `*.Package` projects cover PR CI, tag/manual releases, package verification and publishing. A contributor can submit changes for PR CI. The release workflow checks maintainer actor/triggering actor and uses NuGet and GitHub credentials; companion sync uses a separate token. Protect package bits and identity, registry credentials, and write access to the Docs/Samples repositories. |
| **Repository Metadata and Docs** | Fallback for paths without a more specific owner, including `docs/`, `assets/`, root documentation and repository metadata, and `.github/ISSUE_TEMPLATE/`. These paths do not execute in consumer builds or apps; assess their effect on contributor and release decisions, such as unsafe setup guidance. |

Classify compiler tooling and packaging/build/CI paths before Feature runtimes. Feature tests map to the component they exercise; the shared Roslyn harness (`Observables.TestSupport` and its tests) maps to **Compiler Tooling**. Use **Repository Metadata and Docs** if no other component applies.

## Relevant attackers

- An untrusted or compromised peer/server, or an actor with publish permission on a subscribed broker destination. Reachability depends on the consuming application's endpoints, network and ACLs.
- An app user who controls arguments that the app passes to a client proxy. That reachability depends on the app; the library does not implement public sign-up, app-level authentication, or tenant isolation.
- An external source contributor, compromised dependency/action, or actor with access to a collaborator or release credential. PR CI and publishing are distinct trust boundaries. A maintainer or pre-existing in-process code already has privileges; note when a finding needs them rather than assuming a remote actor has them.

## Observed controls and responsibilities

- REST request construction has escaping helpers; the authorization handler and async-local call scope are separate from application authorization policy. The legacy `AuthenticatedHttpClientHandler` overload installs a default `HttpClientHandler` if none is supplied, so its redirect, cookie, and TLS defaults are in scope alongside app-provided handlers and SDK/client settings.
- SSE has configurable line/event byte limits. The subscription that starts a WebSocket receive pump sets its message limit; later subscriptions on that pump cannot tighten it.
- PostgreSQL validates names for `LISTEN` (including its `UNLISTEN` cleanup) and `pg_notify` against the `[A-Za-z_][A-Za-z0-9_]*` pattern and a 63-character limit. It also quotes LISTEN/UNLISTEN identifiers and binds notification parameters.
- CI checks PRs with read-only content permissions, and CI/release actions use pinned commit SHAs. Release invokes Nuke tests and `PackVerify` before publishing; `PackVerify` checks package contents and backend boundaries, not all supply-chain threats.

## Owner decisions and open questions

- The owner selected the whole Observables repository. No separate infrastructure repository, accepted risks, excluded actors/surfaces, or special asset priority were designated. The scan's severity should stand absent new owner input. Use the **Component** names above for finding tags.
- Consumer service topology, transport policies and broker ACLs, and GitHub branch/tag protection, release-environment policy and credential scopes are not verified from repository code. The model assumes no such protections beyond what the repository shows; report any deployment-dependent prerequisites as conditional.
