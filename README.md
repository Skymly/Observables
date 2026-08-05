# Observables

> **Roslyn source generators bridging events & IO boundaries to R3 / System.Reactive.** Declarative interface-driven proxies for HTTP, SignalR, MQTT, WebSocket, gRPC, SSE, NATS, PostgreSQL LISTEN/NOTIFY, and Redis Pub/Sub — write the interface, get the `Observable<T>`.

[![NuGet](https://img.shields.io/nuget/v/Observables.Events.R3.svg?label=NuGet&logo=nuget)](https://www.nuget.org/profiles/Skymly)
[![CI](https://img.shields.io/github/actions/workflow/status/Skymly/Observables/ci.yml?branch=main&label=CI&logo=github)](https://github.com/Skymly/Observables/actions/workflows/ci.yml)
[![License](https://img.shields.io/github/license/Skymly/Observables.svg?label=License&logo=opensourcehardware)](LICENSE)

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件：用声明式接口把 .NET 事件、HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS、PostgreSQL LISTEN/NOTIFY、Redis Pub/Sub 等边界桥接到 **R3** 或 **System.Reactive**。

| 资源 | 链接 |
|------|------|
| 源码 | [github.com/Skymly/Observables](https://github.com/Skymly/Observables) |
| 用户文档 | [Observables.Docs](https://skymly.github.io/Observables.Docs/) |
| 真实应用 Showcase | [GitPulse](https://github.com/Skymly/GitPulse)（.NET MAUI · `Observables.RestAPI.R3` + `Observables.Events.R3`） |
| 示例应用 | [Observables.Samples](https://github.com/Skymly/Observables.Samples) |
| NuGet | [nuget.org/profiles/Skymly](https://www.nuget.org/profiles/Skymly) |
| 贡献与发版 | [CONTRIBUTING.md](./CONTRIBUTING.md) |

## 运行时与包名

每个功能域成对发布，**R3** 与 **System.Reactive** 互不混用依赖：

| NuGet 包 ID | 运行时 |
|-------------|--------|
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |

开发与测试阶段用解决方案内项目（如 `Observables.Events.R3.SourceGenerators`）通过 `ProjectReference` + `OutputItemType="Analyzer"` 引用。

## 快速开始

在 [NuGet](https://www.nuget.org/profiles/Skymly) 安装对应域的包（当前稳定版见 [CONTRIBUTING.md](./CONTRIBUTING.md#releases-and-versioning)），并单独引用反应式后端：

```powershell
dotnet add package Observables.Events.R3
dotnet add package R3
```

System.Reactive 路径将 `Observables.Events.R3` 换为 `Observables.Events.Reactive`，并添加 `System.Reactive` 包。

从 GitHub Packages 安装或贡献、发版流程见 [CONTRIBUTING.md](./CONTRIBUTING.md)。

## 功能域

十域均已在主仓提供运行时（按需）、双路源生成器与测试；**`0.1.8`** 将十域 **20 包**（含 **Redis**）发至 nuget.org。共享层另有 `Observables.Core`、`Observables.Analyzers`、`Observables.CodeFixes`。

| 域 | 说明 |
|----|------|
| **Events** | 经典与路由 .NET 事件 → `Observable` / `IObservable` |
| **RestAPI** | 声明式类型安全 HTTP 客户端 |
| **SignalR** | Hub 成员代理 |
| **Mqtt** | MQTT 主题代理 |
| **WebSocket** | WebSocket 客户端代理 |
| **Grpc** | `CallInvoker` 代理 |
| **Sse** | `text/event-stream` 事件流 |
| **Nats** | Core NATS subject 代理（v1 不含 JetStream） |
| **Postgres** | PostgreSQL LISTEN/NOTIFY 通道代理（专用非池化连接；推荐 keepalive） |
| **Redis** | Redis 经典 Pub/Sub 通道代理（exact Channel + Pattern；`RedisMessage<T>` 信封） |

设计稿见 [`docs/`](docs/README.md)（文档体系见 [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)）。路由事件生成默认关闭；在消费者项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`（见 `Observables.Events/Observables.Events/targets/observables.events.props`）。

### 仓库结构（摘要）

| 层级 | 说明 |
|------|------|
| **`Observables.Core`** | 全库通用运行时 |
| **`Observables.SourceGenerators.Shared`** | 全库通用生成器基础设施（link-compile 共享源码） |
| **`Observables.<Feature>`** | 域运行时（按需；纯生成域如 Events 可不建） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 桥接运行时（按需） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 双路源生成器 |
| **`Observables.<Feature>.Package`** | 发布打包，产出上述两个 NuGet 包 |

## Events

经典 .NET 事件 → `Observable<T>` / `IObservable<T>`。无需定义接口——对任何含 `event` 成员的类型调用 `.Events()` 扩展方法，生成器自动为每个事件产出对应的 Observable 属性。

### 基本用法

```csharp
// 任何含 event 的类或接口都可以使用
public class Button
{
    public event EventHandler? Clicked;
    public event Action<string>? TextChanged;
}

// R3 路径
var btn = new Button();
using var d1 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
using var d2 = btn.Events().TextChanged.Subscribe(text => Console.WriteLine(text));

// System.Reactive 路径
using var d3 = btn.Events().Clicked.Subscribe(_ => Console.WriteLine("Clicked!"));
```

### Events vs EventHandlers

- **`.Events()`** — 将事件参数直接映射为 `Observable<(T1, T2)>` 元组（去掉 `sender`），适合只关心载荷的场景。
- **`.EventHandlers()`** — 保留 `(sender, EventArgs)` 元组形状，适合需要 sender 的场景。

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

### 路由事件（WPF / Avalonia）

默认关闭。在消费者项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>` 启用，生成器会为路由事件产出 `.RoutedEvents()` / `.RoutedEventHandlers()` 扩展方法。详见 `Observables.Events/Observables.Events/targets/observables.events.props`。

设计稿见 `docs/design/events.md`。

## RestAPI

声明式类型安全 HTTP 客户端。用 `[Get]` / `[Post]` / `[Put]` / `[Delete]` / `[Patch]` 等特性标注接口方法，生成器自动产出 `HttpClient` 代理实现。支持路径模板 `{param}`、JSON 序列化、`Observable<T>` / `IObservable<T>` / `Task<T>` 返回类型。

### 基本用法

```csharp
// 定义接口 — 无需手写 HttpClient 调用
public interface IUserApi
{
    [Get("/users/{id}")]
    Observable<User> GetUser(int id);          // R3 路径

    [Post("/users")]
    Observable<User> CreateUser([Body] User user);

    [Delete("/users/{id}")]
    Observable<Unit> DeleteUser(int id);
}

// 创建代理
var api = RestService.For<IUserApi>("https://api.example.com");
using var d = api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### System.Reactive 路径

将 `Observable<T>` 换为 `IObservable<T>`，并引用 `Observables.RestAPI.Reactive` 包：

```csharp
public interface IUserApi
{
    [Get("/users/{id}")]
    IObservable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>("https://api.example.com");
api.GetUser(42).Subscribe(u => Console.WriteLine(u.Name));
```

### 包结构

| 包 | 用途 |
|----|------|
| `Observables.RestAPI` | 运行时（`RestService`、`HttpClient` 代理基础设施） |
| `Observables.RestAPI.R3` | R3 源生成器 + 运行时 |
| `Observables.RestAPI.Reactive` | System.Reactive 桥接运行时 + 源生成器 |

该域的运行时部分包含由 [Refit](https://github.com/reactiveui/refit) 适配而来的代码，许可信息见 [NOTICE.md](NOTICE.md)。

## SignalR

在 `[Hub]` 接口上用 `[HubInvoke]` / `[HubOn]` / `[HubSend]` / `[HubStream]` 标注成员，生成器产出 SignalR Hub 代理。`HubInvoke` 映射方法调用，`HubOn` 映射服务器推送事件。

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

依赖 [Microsoft.AspNetCore.SignalR.Client](https://www.nuget.org/packages/Microsoft.AspNetCore.SignalR.Client)。

## Mqtt

在 `[Mqtt]` 接口上用 `[MqttSubscribe]` 标注属性（订阅热流）、`[MqttPublish]` 标注方法（发布冷流），生成器产出 MQTT 主题代理。

```csharp
[Mqtt]
public interface ISensorHub
{
    [MqttSubscribe("sensors/temperature/#")]
    Observable<SensorData> Temperature { get; }

    [MqttPublish("commands/{deviceId}/reboot")]
    Observable<Unit> Reboot(string deviceId);
}

var hub = MqttService.For<ISensorHub>(mqttClient);
using var d = hub.Temperature.Subscribe(data => Console.WriteLine(data.Value));
```

依赖 [MQTTnet](https://www.nuget.org/packages/MQTTnet)。

## WebSocket

在 `[WebSocket]` 接口上用 `[WebSocketReceive]` 标注属性（接收消息）、`[WebSocketSend]` / `[WebSocketConnect]` / `[WebSocketClose]` 标注方法，生成器产出 WebSocket 代理。

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

在 `[Grpc]` 接口上用 `[GrpcUnary]` / `[GrpcServerStream]` / `[GrpcClientStream]` / `[GrpcDuplex]` 标注方法，生成器产出 `CallInvoker` 代理。

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

依赖 [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client)。

## Sse（Server-Sent Events）

在 `[Sse]` 接口上用 `[SseEvent("名称")]` 标注属性，生成器产出按事件名过滤、自动反序列化的 `Observable<T>` / `IObservable<T>` 代理。`[SseEvent]` 不带参数时映射默认 `message` 事件。

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

每次 `Subscribe` 发起一次 SSE 连接；`string` 直接透传，其它类型按 `System.Text.Json` 反序列化。

## Nats（Core NATS）

在 `[Nats]` 接口上用 `[NatsSubscribe]` / `[NatsPublish]` / `[NatsRequest]` 标注成员，生成器产出订阅热流、发布冷流与请求-响应单值流。

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

依赖 [NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core)。

## Postgres（LISTEN/NOTIFY）

在 `[Postgres]` 接口上用 `[Listen]` / `[Notify]` 标注成员，生成器产出 LISTEN 热流与 NOTIFY 冷流。`PostgresService.For<T>` 接受**专用、非池化**的 `NpgsqlConnection`（勿从连接池借连接做长生命周期 `Wait`）；Listen 连接建议 `Pooling=false` 并设置 Npgsql **`Keepalive`**。

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

依赖 [Npgsql](https://www.nuget.org/packages/Npgsql)。维护者设计说明见 [`docs/design/postgres.md`](docs/design/postgres.md)。

## Redis（Pub/Sub）

在 `[Redis]` 接口上用 `[RedisSubscribe]` / `[RedisPublish]` 标注成员，生成器产出 Subscribe 热流与 Publish 冷流。含 `*` / `?` 的 Channel 走 Pattern（`PSUBSCRIBE`）；返回 `Observable<RedisMessage<T>>` 时附带具体 Channel。`RedisService.For<T>` 接受 `IConnectionMultiplexer`。

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

依赖 [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis)。维护者设计说明见 [`docs/design/redis.md`](docs/design/redis.md)。v1 **不含** Streams / keyspace / sharded Pub/Sub。

## 构建

```powershell
cd Observables
dotnet build Observables.slnx

# 完整 CI（Nuke，与 GitHub Actions 一致）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

需 **.NET 10 SDK**（`global.json` 用于 Nuke `build/`）；库与测试目标为 **netstandard2.0** / **net8.0** 等，另需 **.NET 8 SDK**。本地打包、发版与贡献规范见 [CONTRIBUTING.md](./CONTRIBUTING.md)。

## License

Observables is licensed under the MIT License — see [LICENSE](LICENSE).

Third-party attributions, including the Refit-derived code used by `Observables.RestAPI`, are listed in [NOTICE.md](NOTICE.md).
