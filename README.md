# Observables

**English** | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md)

> **Roslyn source generators bridging events & IO boundaries to R3 / System.Reactive.** Declarative interface-driven proxies for HTTP, SignalR, MQTT, WebSocket, gRPC, SSE, NATS, PostgreSQL LISTEN/NOTIFY, and Redis Pub/Sub — write the interface, get the `Observable<T>`.

[![NuGet](https://img.shields.io/nuget/v/Observables.Events.R3.svg?label=NuGet&logo=nuget)](https://www.nuget.org/profiles/Skym)
[![CI](https://img.shields.io/github/actions/workflow/status/Skymly/Observables/ci.yml?branch=main&label=CI&logo=github)](https://github.com/Skymly/Observables/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Skymly/Observables.svg?label=License&logo=opensourcehardware)](LICENSE)

A Roslyn source-generator suite for **reactive programming (Rx)**: declaratively bridge .NET events, HTTP, SignalR, MQTT, WebSocket, gRPC, SSE, NATS, PostgreSQL LISTEN/NOTIFY, and Redis Pub/Sub boundaries into **R3** or **System.Reactive**.

| Resource | Link |
|----------|------|
| Source | [github.com/Skymly/Observables](https://github.com/Skymly/Observables) |
| Documentation | [Observables.Docs](https://skymly.github.io/Observables.Docs/) |
| Production Showcase | [GitPulse](https://github.com/Skymly/GitPulse) (.NET MAUI · `Observables.RestAPI.R3` + `Observables.Events.R3`) |
| Sample Apps | [Observables.Samples](https://github.com/Skymly/Observables.Samples) |
| NuGet | [nuget.org/profiles/Skym](https://www.nuget.org/profiles/Skym) |
| Contributing & Releases | [CONTRIBUTING.md](./CONTRIBUTING.md) |
| Security Model | [SECURITY_MODEL.md](./SECURITY_MODEL.md) |

## Runtimes and Package Names

Each feature domain ships in pairs, keeping **R3** and **System.Reactive** dependencies strictly isolated:

| NuGet Package ID | Runtime |
|------------------|---------|
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive) (`IObservable<T>`, etc.) |

During in-solution development and testing, reference the generator project directly (for example `Observables.Events.R3.SourceGenerators`) via `ProjectReference` with `OutputItemType="Analyzer"`.

## Quick Start

Install the desired domain package from [NuGet](https://www.nuget.org/profiles/Skym) (see [CONTRIBUTING.md](./CONTRIBUTING.md#releases-and-versioning) for the current stable version), and add the reactive backend package separately:

```powershell
dotnet add package Observables.Events.R3
dotnet add package R3
```

For the System.Reactive path, swap `Observables.Events.R3` for `Observables.Events.Reactive` and add the `System.Reactive` package.

See [CONTRIBUTING.md](./CONTRIBUTING.md) for GitHub Packages installation, contributing guidelines, and the release workflow.

## Feature Domains

All ten feature domains provide dual-backend source generators and tests, plus runtimes where needed. The shared layer also includes `Observables.Core`, `Observables.Analyzers`, and `Observables.CodeFixes`.

The MQTT domain ships with MQTTnet 4/5 adapter packages and a neutral MQTT runtime. RestAPI, MQTT, and Redis support source-generated JSON metadata on .NET 8 and later.

| Domain | Description |
|--------|-------------|
| **Events** | Classic and routed .NET events → `Observable` / `IObservable` |
| **RestAPI** | Declarative type-safe HTTP client |
| **SignalR** | Hub member proxies |
| **Mqtt** | MQTT topic proxies |
| **WebSocket** | WebSocket client proxies |
| **Grpc** | `CallInvoker` proxies |
| **Sse** | `text/event-stream` event streams |
| **Nats** | Core NATS subject proxies (v1 excludes JetStream) |
| **Postgres** | PostgreSQL LISTEN/NOTIFY channel proxies (dedicated unpooled connection; `Keepalive` recommended) |
| **Redis** | Redis classic Pub/Sub channel proxies (exact Channel + Pattern; `RedisMessage<T>` envelope) |

See design docs in [`docs/`](docs/README.md) (documentation map in [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)). Routed-event generation is disabled by default; set `<ObservableRoutedEvents>true</ObservableRoutedEvents>` in the consumer project (see `Observables.Events/Observables.Events/targets/observables.events.props`).

### Repository Structure (Summary)

| Layer | Purpose |
|-------|---------|
| **`Observables.Core`** | Solution-wide runtime |
| **`Observables.SourceGenerators.Shared`** | Solution-wide generator infrastructure (link-compiled shared source) |
| **`Observables.<Feature>`** | Domain runtime (when needed; pure generator domains like Events omit this) |
| **`Observables.<Feature>.Reactive`** | System.Reactive bridge runtime (when needed) |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | Dual-backend source generators |
| **`Observables.<Feature>.Package`** | Packing project producing the two NuGet packages above |

## Events

Classic .NET events → `Observable<T>` / `IObservable<T>`. No interface definition required — call `.Events()` on any type with `event` members, and the generator produces an Observable property for each event.

### Basic Usage

```csharp
// Works with any class or interface that declares events
public class Button
{
    public event EventHandler? Clicked;
    public event Action<string>? TextChanged;
}

// R3 path
var btn = new Button();
using var d1 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
using var d2 = btn.Events().TextChanged.Subscribe(text => Console.WriteLine(text));

// System.Reactive path
using var d3 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
```

### Events vs EventHandlers

- **`.Events()`** — Projects event arguments directly into `Observable<(T1, T2)>` tuples (omitting `sender`), ideal when you only care about the payload.
- **`.EventHandlers()`** — Preserves the `(sender, EventArgs)` tuple shape for cases where `sender` is needed.

```csharp
public interface INotifyPropertyChanged
{
    event PropertyChangedEventHandler? PropertyChanged;
}

// Events() → Observable<PropertyChangedEventArgs>
_ = obj.Events().PropertyChanged.Subscribe(e => Console.WriteLine(e.PropertyName));

// EventHandlers() → Observable<(object sender, PropertyChangedEventArgs e)>
_ = obj.EventHandlers().PropertyChanged.Subscribe(t => Console.WriteLine(t.e.PropertyName));
```

### Routed Events (WPF / Avalonia)

Disabled by default. Enable it in the consumer project with `<ObservableRoutedEvents>true</ObservableRoutedEvents>` so the generator emits `.RoutedEvents()` / `.RoutedEventHandlers()` extension methods for routed events. See `Observables.Events/Observables.Events/targets/observables.events.props`.

See design doc `docs/design/events.md`.

## RestAPI

Declarative type-safe HTTP client. Annotate interface methods with `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Patch]`, etc., and the generator emits an `HttpClient` proxy implementation. Supports `{param}` route templates and JSON serialization. Return types depend on the chosen package: `Observables.RestAPI.R3` uses `Observable<T>` / `Task<T>` (the R3 generator rejects `IObservable<T>`); `Observables.RestAPI.Reactive` uses `IObservable<T>` / `Task<T>`.

### Basic Usage

```csharp
// Define the interface — no boilerplate HttpClient calls
public interface IUserApi
{
    [Get("/users/{id}")]
    Observable<User> GetUser(int id);          // R3 path

    [Post("/users")]
    Observable<User> CreateUser([Body] User user);

    [Delete("/users/{id}")]
    Observable<Unit> DeleteUser(int id);
}

// Create the proxy
var api = RestService.For<IUserApi>("https://api.example.com");
using var d = api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### System.Reactive Path

Replace `Observable<T>` with `IObservable<T>` and reference the `Observables.RestAPI.Reactive` package:

```csharp
public interface IUserApi
{
    [Get("/users/{id}")]
    IObservable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>("https://api.example.com");
api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### NuGet Installation

Choose a NuGet package for your reactive backend. Both packages bundle the runtime needed by `RestService` and the `HttpClient` proxy.

| NuGet Package ID | Bundled Runtime Assemblies | Source Generator |
|------------------|----------------------------|------------------|
| [`Observables.RestAPI.R3`](https://www.nuget.org/packages/Observables.RestAPI.R3) | `Observables.RestAPI.dll` | R3 |
| [`Observables.RestAPI.Reactive`](https://www.nuget.org/packages/Observables.RestAPI.Reactive) | `Observables.RestAPI.dll`, `Observables.RestAPI.Reactive.dll` | System.Reactive |

The runtime portion of this domain incorporates code adapted from [Refit](https://github.com/reactiveui/refit); see [NOTICE.md](NOTICE.md) for license attributions.

## SignalR

Annotate members on a `[Hub]` interface with `[HubInvoke]`, `[HubOn]`, `[HubSend]`, or `[HubStream]`, and the generator produces a SignalR Hub proxy. `HubInvoke` maps method calls and `HubOn` maps server-pushed events.

```csharp
[Hub]
public interface IChatHub
{
    [HubInvoke]
    Observable<int> GetUserCount();

    [HubOn("ReceiveMessage")]
    Observable<ChatMessage> ReceiveMessage { get; }
}

var hub = HubService.For<IChatHub>(hubConnection);
using var d = hub.ReceiveMessage.Subscribe(msg => Console.WriteLine(msg.Text));
```

Depends on [Microsoft.AspNetCore.SignalR.Client](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR.Client).

## Mqtt

Annotate subscription properties with `[MqttSubscribe]` and publish methods with `[MqttPublish]` on an `[Mqtt]` interface. Install either `Observables.Mqtt.MQTTnet4` or `Observables.Mqtt.MQTTnet5` to match your MQTTnet client major version and import its namespace.

```csharp
[Mqtt]
public interface ISensorHub
{
    [MqttSubscribe("sensors/temperature/#")]
    Observable<SensorData> Temperature { get; }

    [MqttPublish("commands/{deviceId}/reboot")]
    Observable<Unit> Reboot(string deviceId);
}

var hub = MqttService.For<ISensorHub>(MqttNetTransport.Wrap(mqttClient));
using var d = hub.Temperature.Subscribe(data => Console.WriteLine(data.Value));
```

The MQTTnet 5 adapter package requires .NET 8 or later. The application remains responsible for connecting and disposing the client. [MQTT design & migration notes](docs/design/mqtt.md).

## WebSocket

Annotate receive properties with `[WebSocketReceive]` and control/send methods with `[WebSocketSend]`, `[WebSocketConnect]`, or `[WebSocketClose]` on a `[WebSocket]` interface, and the generator produces a WebSocket proxy.

```csharp
[WebSocket]
public interface IRealtimeHub
{
    [WebSocketReceive("tick")]
    Observable<Tick> Ticks { get; }

    [WebSocketSend("subscribe")]
    Observable<Unit> Subscribe(string channel);
}

var hub = WebSocketService.For<IRealtimeHub>(clientWebSocket);
using var d = hub.Ticks.Subscribe(tick => Console.WriteLine(tick.Price));
```

## Grpc

Annotate methods on a `[Grpc]` interface with `[GrpcUnary]`, `[GrpcServerStream]`, `[GrpcClientStream]`, or `[GrpcDuplex]`, and the generator produces a `CallInvoker` proxy.

```csharp
[Grpc("echo.Echo")]
public interface IEchoService
{
    [GrpcUnary("UnaryEcho")]
    Observable<string> UnaryEcho(string request);

    [GrpcServerStream("ServerStreamEcho")]
    Observable<string> ServerStreamEcho(string request);
}

var svc = GrpcService.For<IEchoService>(callInvoker);
using var d = svc.UnaryEcho("hello").Subscribe(reply => Console.WriteLine(reply));
```

Both gRPC backend packages depend on `Grpc.Core.Api`. The example above takes an application-supplied `CallInvoker`, leaving transport choice to the application. When creating an invoker via `GrpcChannel.ForAddress(...).CreateCallInvoker()`, install [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client).

## Sse (Server-Sent Events)

Annotate properties on an `[Sse]` interface with `[SseEvent("name")]`, and the generator emits an `Observable<T>` / `IObservable<T>` proxy that filters by event name and deserializes payloads automatically. `[SseEvent]` without arguments maps the default `message` event.

```csharp
[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<PriceTick> Prices { get; }

    [SseEvent]
    Observable<string> Heartbeats { get; }
}

var feed = SseService.For<IPriceFeed>(new SseConnection(httpClient, endpoint));
using var d = feed.Prices.Subscribe(tick => Console.WriteLine(tick));
```

Each `Subscribe` opens an SSE connection; `string` passes through directly, while other types are deserialized with `System.Text.Json`.

## Nats (Core NATS)

Annotate members on a `[Nats]` interface with `[NatsSubscribe]`, `[NatsPublish]`, or `[NatsRequest]`, and the generator produces hot subscription streams, cold publish streams, and single-value request-reply streams.

```csharp
[Nats]
public interface IOrderHub
{
    [NatsSubscribe("orders.>")]
    Observable<OrderEvent> OrderEvents { get; }

    [NatsPublish("orders.{id}.cancel")]
    Observable<Unit> Cancel(string id);

    [NatsRequest("orders.validate")]
    Observable<ValidationResult> Validate(OrderRequest request);
}

await using var nats = new NatsConnection(new NatsOpts { Url = "nats://127.0.0.1:4222" });
var hub = NatsService.For<IOrderHub>(nats);
```

Depends on [NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core).

## Postgres (LISTEN/NOTIFY)

Annotate members on a `[Postgres]` interface with `[Listen]` or `[Notify]`, and the generator produces hot LISTEN streams and cold NOTIFY streams. `PostgresService.For<T>` accepts a **dedicated, unpooled** `NpgsqlConnection` (never borrow a pooled connection for long-lived `Wait` loops); set `Pooling=false` and configure Npgsql **`Keepalive`** on the LISTEN connection.

```csharp
[Postgres]
public interface IOrderHub
{
    [Listen("orders")]
    Observable<string> Orders { get; }

    [Notify("orders")]
    Observable<Unit> PublishOrder(string payload, CancellationToken cancellationToken = default);
}

await using var connection = new NpgsqlConnection(
    "Host=localhost;Database=app;Username=app;Password=…;Pooling=false;Keepalive=30");
await connection.OpenAsync();
var hub = PostgresService.For<IOrderHub>(connection);
```

Depends on [Npgsql](https://www.nuget.org/packages/Npgsql). See maintainer design notes at [`docs/design/postgres.md`](docs/design/postgres.md).

## Redis (Pub/Sub)

Annotate members on a `[Redis]` interface with `[RedisSubscribe]` or `[RedisPublish]`, and the generator produces hot Subscribe streams and cold Publish streams. Channels containing `*` or `?` use pattern subscriptions (`PSUBSCRIBE`); returning `Observable<RedisMessage<T>>` includes the matched channel name. `RedisService.For<T>` accepts an `IConnectionMultiplexer`.

```csharp
[Redis]
public interface INewsHub
{
    [RedisSubscribe("news.sports")]
    Observable<string> Sports { get; }

    [RedisSubscribe("news.*")]
    Observable<RedisMessage<string>> NewsFamily { get; }

    [RedisPublish("news.{topic}")]
    Observable<Unit> Publish(string topic, string payload, CancellationToken cancellationToken = default);
}

await using var mux = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var hub = RedisService.For<INewsHub>(mux);
```

Depends on [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis). See maintainer design notes at [`docs/design/redis.md`](docs/design/redis.md). v1 **excludes** Streams, keyspace notifications, and sharded Pub/Sub.

## Build

```powershell
cd Observables
dotnet build Observables.slnx

# Full CI (Nuke, matching GitHub Actions)
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

The full Nuke `Ci` target requires **.NET 8, .NET 9, and .NET 10 SDKs** along with their runtimes / testhosts. `global.json` pins Nuke `build/` to the **.NET 10 SDK** (`rollForward` is `latestFeature` within the same major version only). Libraries target **netstandard2.0** / **net8.0** / **net9.0** / **net10.0**; runtime E2E tests execute against **net8.0, net9.0, and net10.0** by default, making the **.NET 9 testhost an explicit requirement** — installing only 8 + 10 is not sufficient, and cross-major `DOTNET_ROLL_FORWARD` **cannot** substitute for native net9 tests. This is a contributor local environment contract, not a library packaging bug. See [CONTRIBUTING.md](./CONTRIBUTING.md) for local packing, release workflow, and contribution guidelines.

## License

Observables is licensed under the MIT License — see [LICENSE](LICENSE).

Third-party attributions, including the Refit-derived code used by `Observables.RestAPI`, are listed in [NOTICE.md](NOTICE.md).
