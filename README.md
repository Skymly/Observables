# Observables

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件：用声明式接口把 .NET 事件、HTTP、SignalR、MQTT、WebSocket、gRPC、SSE、NATS 等边界桥接到 **R3** 或 **System.Reactive**。

| 资源 | 链接 |
|------|------|
| 源码 | [github.com/Skymly/Observables](https://github.com/Skymly/Observables) |
| 用户文档 | [Observables.Docs](https://skymly.github.io/Observables.Docs/) |
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

八域均已提供运行时（按需）、双路源生成器、测试与 NuGet 包；共享层另有 `Observables.Core`、`Observables.SourceGenerators.Shared`、`Observables.Analyzers`、`Observables.CodeFixes`。

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

设计稿见 `docs/design/`。路由事件生成默认关闭；在消费者项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`（见 `Observables.Events/Observables.Events/targets/observables.events.props`）。

### 仓库结构（摘要）

| 层级 | 说明 |
|------|------|
| **`Observables.Core`** | 全库通用运行时 |
| **`Observables.SourceGenerators.Shared`** | 全库通用生成器基础设施 |
| **`Observables.<Feature>`** | 域运行时（按需；纯生成域如 Events 可不建） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 桥接运行时（按需） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 双路源生成器 |
| **`Observables.<Feature>.Package`** | 发布打包，产出上述两个 NuGet 包 |

## RestAPI

声明式类型安全 HTTP 客户端：`Observables.RestAPI`（运行时）+ `Observables.RestAPI.R3.SourceGenerators` 或 `Observables.RestAPI.Reactive.SourceGenerators` + 可选 `Observables.RestAPI.Reactive` / `HttpClientFactory`。

该域的运行时部分包含由 [Refit](https://github.com/reactiveui/refit) 适配而来的代码，许可信息见 [NOTICE.md](NOTICE.md)。

```xml
<ProjectReference Include="Observables.RestAPI" />
<ProjectReference Include="Observables.RestAPI.R3.SourceGenerators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Sse（Server-Sent Events）

在 `[Sse]` 接口上用 `[SseEvent("名称")]` 标注属性，生成器产出按事件名过滤、自动反序列化的 `Observable<T>` / `IObservable<T>` 代理。`[SseEvent]` 不带参数时映射默认 `message` 事件。

```csharp
[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<PriceTick> Prices { get; }   // System.Reactive 用 IObservable<T>

    [SseEvent]
    Observable<string> Heartbeats { get; }  // 默认 message 事件
}

var feed = SseService.For<IPriceFeed>(new SseConnection(httpClient, endpoint));
using var d = feed.Prices.Subscribe(tick => Console.WriteLine(tick));
```

每次 `Subscribe` 发起一次 SSE 连接；`string` 直接透传，其它类型按 `System.Text.Json` 反序列化。设计稿见 [docs/design/sse.md](docs/design/sse.md)。

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

依赖 [NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core)。设计稿见 [docs/design/nats.md](docs/design/nats.md)。

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
