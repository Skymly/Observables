# Observables

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件。远端仓库：[github.com/Skymly/Observables](https://github.com/Skymly/Observables)（公开）。

里程碑与发版规划见 [docs/ROADMAP.md](docs/ROADMAP.md)；开发规范与工程治理见 [AGENTS.md](./AGENTS.md)。

## 运行时与包名

| NuGet 包 ID | 运行时 |
|-------------|--------|
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |

每个功能域成对发布，互不混用依赖。开发与测试阶段用解决方案内项目（如 `Observables.Events.R3.SourceGenerators`）通过 `ProjectReference` + `OutputItemType="Analyzer"` 引用。

## 全库与域结构

| 层级 | 说明 |
|------|------|
| **`Observables.Core`** | 全库通用运行时（多域复用的 Attribute、枚举、接口等） |
| **`Observables.SourceGenerators.Shared`** | 全库通用生成器基础设施（诊断、符号扩展等） |
| **`Observables.<Feature>`** | 域运行时（按需；纯生成域如 Events 可不建） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 桥接运行时（按需） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 双路源生成器 |
| **`Observables.<Feature>.Package`** | 发布时打包，产出上述两个 NuGet 包 |

### NuGet（`0.1.0` 稳定版）

**16 包**（八域各 `.R3` + `.Reactive`）。**`0.1.0`** 发布至 [nuget.org](https://www.nuget.org/profiles/Skymly) 与 GitHub Packages（tag `v0.1.0`，含 Nats 域）。

| 包 ID | 说明 |
|-------|------|
| `Observables.Events.R3` | Events 生成器 + R3 依赖（DevelopmentDependency） |
| `Observables.Events.Reactive` | Events 生成器 + System.Reactive 依赖 |
| `Observables.RestAPI.R3` | RestAPI 运行时 + R3 生成器 |
| `Observables.RestAPI.Reactive` | RestAPI + Reactive 桥接 + 生成器 |
| `Observables.SignalR.R3` | SignalR 运行时 + R3 生成器 |
| `Observables.SignalR.Reactive` | SignalR + Reactive 桥接 + 生成器 |
| `Observables.Mqtt.R3` | MQTT 运行时 + R3 生成器 |
| `Observables.Mqtt.Reactive` | MQTT + Reactive 桥接 + 生成器 |
| `Observables.WebSocket.R3` | WebSocket 运行时 + R3 生成器 |
| `Observables.WebSocket.Reactive` | WebSocket + Reactive 桥接 + 生成器 |
| `Observables.Grpc.R3` | gRPC 运行时 + R3 生成器 |
| `Observables.Grpc.Reactive` | gRPC + Reactive 桥接 + 生成器 |
| `Observables.Sse.R3` | SSE 运行时 + R3 生成器 |
| `Observables.Sse.Reactive` | SSE + Reactive 桥接 + 生成器 |
| `Observables.Nats.R3` | NATS 运行时 + R3 生成器 |
| `Observables.Nats.Reactive` | NATS + Reactive 桥接 + 生成器 |

```xml
<PackageReference Include="Observables.Events.R3" Version="0.1.0" />
<PackageReference Include="R3" Version="1.3.0" />
```

从 [GitHub Packages](https://github.com/orgs/Skymly/packages) 安装时，在 `nuget.config` 中增加：

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

本地打包与校验：

```powershell
dotnet run --project build/_build.csproj -- --target PackVerify --configuration Release
```

**发布到 NuGet**（与 [MvvmAIO.Markup](https://github.com/MvvmAIO/MvvmAIO.Markup) 相同：由维护者推送 `v*` tag 触发 CI，而非 PR/main 自动发布）：

```powershell
# 1. 确认 eng/Observables.Package.props 中 PackageVersion 与 tag 一致
git tag -a v0.1.0 -m "0.1.0"
git push origin v0.1.0
# 2. GitHub Actions「Publish NuGet」workflow 使用 secrets 执行 Publish
```

本地手动推送（可选）：

```powershell
$env:VERSION = '0.1.0'
$env:NUGET_API_KEY = '...'
$env:GITHUB_TOKEN = '...'
dotnet run --project build/_build.csproj -- --target Publish --configuration Release
```

## 域实现状态

| 域 | R3 生成器 | System.Reactive 生成器 | NuGet（`0.1.0`） |
|----|-----------|------------------------|---------------------|
| **Events**（经典 + 路由 .NET 事件） | `Events.R3.SourceGenerators` | `Events.Reactive.SourceGenerators` | 已纳入发版 |
| **RestAPI**（声明式 HTTP 客户端） | `RestAPI.R3.SourceGenerators` | `RestAPI.Reactive.SourceGenerators` | 已纳入发版 |
| **SignalR**（Hub 代理） | `SignalR.R3.SourceGenerators` | `SignalR.Reactive.SourceGenerators` | 已纳入发版 |
| **Mqtt**（主题代理） | `Mqtt.R3.SourceGenerators` | `Mqtt.Reactive.SourceGenerators` | 已纳入发版 |
| **WebSocket**（客户端代理） | `WebSocket.R3.SourceGenerators` | `WebSocket.Reactive.SourceGenerators` | 已纳入发版 |
| **Grpc**（CallInvoker 代理） | `Grpc.R3.SourceGenerators` | `Grpc.Reactive.SourceGenerators` | 已纳入发版 |
| **Sse**（`text/event-stream` 代理） | `Sse.R3.SourceGenerators` | `Sse.Reactive.SourceGenerators` | 已纳入发版（M5） |
| **Nats**（Core NATS subject 代理） | `Nats.R3.SourceGenerators` | `Nats.Reactive.SourceGenerators` | 已纳入发版（M6） |

八域均含运行时 + 双路生成器 + 测试；共享层另有 `Observables.Analyzers` 与 `Observables.CodeFixes`。设计稿见 `docs/design/`；发版顺序见 [docs/ROADMAP.md](docs/ROADMAP.md)。

路由事件生成默认关闭；在消费者项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`（见 `Observables.Events/Observables.Events/targets/observables.events.props`）。

## RestAPI

声明式类型安全 HTTP 客户端：`Observables.RestAPI`（运行时）+ `Observables.RestAPI.R3.SourceGenerators` 或 `Observables.RestAPI.Reactive.SourceGenerators` + 可选 `Observables.RestAPI.Reactive` / `HttpClientFactory`。

```xml
<ProjectReference Include="Observables.RestAPI" />
<ProjectReference Include="Observables.RestAPI.R3.SourceGenerators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Sse（Server-Sent Events）

声明式 `text/event-stream` 客户端：在 `[Sse]` 接口上用 `[SseEvent("名称")]` 标注属性，生成器产出按事件名过滤、自动反序列化的 `Observable<T>` / `IObservable<T>` 代理。`[SseEvent]` 不带参数时映射默认 `message` 事件。

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

声明式 Core NATS subject 客户端：在 `[Nats]` 接口上用 `[NatsSubscribe]` / `[NatsPublish]` / `[NatsRequest]` 标注成员，生成器产出订阅热流、发布冷流与请求-响应单值流。

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

依赖 [NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core)。v1 不含 JetStream。设计稿见 [docs/design/nats.md](docs/design/nats.md)。

## 构建

```powershell
cd Observables
dotnet build Observables.slnx

# 完整 CI（Nuke，与 GitHub Actions 一致）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

需 **.NET 10 SDK**（`global.json` 用于 Nuke `build/`）；库与测试目标为 **netstandard2.0** / **net8.0** 等，另需 **.NET 8 SDK**。

代理与贡献者请参阅 [AGENTS.md](./AGENTS.md)。
