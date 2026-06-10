# 设计：Server-Sent Events（SSE）域

> 状态：M5（`0.1.0-preview7`）设计稿。本文为维护者设计笔记；面向用户的使用文档见 Observables.Docs `sse.md`。

## 1. 动机与定位

`Observables.Sse` 补全「**HTTP 单向流**」边界：

| 域 | HTTP 语义 |
|----|-----------|
| RestAPI | request / response（一次性） |
| **SSE** | **server → client 单向事件流（长连接）** |
| WebSocket | 全双工 |

SSE（`text/event-stream`）是 `IObservable<T>` 的教科书场景：一条 HTTP 长连接持续推送「具名事件」，消费者按事件名订阅。接口面与 SignalR 的 `[HubOn]` 同构——**纯属性、纯消费**，无 client→server 方法。

工程骨架以 **WebSocket 域**为模板（interface-proxy 模式：`[Sse]` 接口 → 生成 `*GeneratedProxy` + `ModuleInitializer` 注册）。

## 2. 公共面（草案）

### 属性

| 属性 | 目标 | 说明 |
|------|------|------|
| `[Sse]` | `interface` | 标记 SSE 代理接口 |
| `[SseEvent(string? eventName = null)]` | `property` | 订阅具名事件流；`eventName` 省略时匹配 SSE 默认事件类型 `"message"` |

SSE 是纯消费流：**接口仅含属性**。出现方法 → 诊断（见 §5）。

### 运行时入口

```csharp
namespace Observables.Sse;

public sealed class SseConnection
{
    public SseConnection(HttpClient httpClient, Uri endpoint);
    public HttpClient HttpClient { get; }
    public Uri Endpoint { get; }
}

public static class SseService
{
    public static T For<T>(SseConnection connection);
}
```

工厂入参为单一 `SseConnection`（绑定 `HttpClient` + 端点 `Uri`），与 WebSocket 的 `For<T>(ClientWebSocket)` 单参一致，使生成的注册委托保持 `Func<SseConnection, object>`。

### 消费者示例

```csharp
[Sse]
public interface IPriceFeed
{
    [SseEvent("price")]
    Observable<PriceTick> Prices { get; }   // R3；Reactive 包用 IObservable<PriceTick>

    [SseEvent]                               // 默认 "message" 事件
    Observable<string> Heartbeats { get; }
}

var conn = new SseConnection(httpClient, new Uri("https://example.com/stream"));
var feed = SseService.For<IPriceFeed>(conn);
feed.Prices.Subscribe(tick => Console.WriteLine(tick.Symbol));
```

## 3. 生成映射

每个 `[SseEvent("name")]` 属性生成：

```csharp
private global::R3.Observable<T>? _Prices;
public global::R3.Observable<T> Prices =>
    _Prices ??= global::Observables.Sse.SseObservable.FromEvent<T>(_connection, "price");
```

- R3 桥：`Observables.Sse.SseObservable.FromEvent<T>`。
- Reactive 桥：`Observables.Sse.Reactive.SystemReactiveSseAdapter.FromEvent<T>`（由 `ObservableReturnTypeParser` 按返回类型选择后端）。
- 注册：`ModuleInitializer` 调 `SseService.RegisterGeneratedFactory(typeof(IPriceFeed), static c => new PriceFeedGeneratedProxy(c))`。

## 4. 协议与运行时

SSE 解析（`text/event-stream`，见 [WHATWG HTML §9.2](https://html.spec.whatwg.org/multipage/server-sent-events.html)）：

- 每次订阅发起一次 `GET`（`Accept: text/event-stream`，`HttpCompletionOption.ResponseHeadersRead`），逐行解析响应流。
- 行格式 `field: value`（值前单个空格可选）；`data` 多行以 `\n` 连接；空行派发一个事件。
- `event` 缺省 → 事件名 `"message"`；`:` 开头为注释（忽略）；`retry` 暂忽略。
- payload 反序列化：`T == string` 直接取 `data`；其他类型走 `System.Text.Json`（**net8+**；`netstandard2.0` 仅支持 `string`，与 WebSocket 一致）。

> **v1（preview7）简化**：每个属性订阅各自开一条连接并按事件名过滤（与 WebSocket `FromReceive` 每订阅一连接一致）。单连接多路复用（一条流广播给多个事件属性）列为后续优化。重连 / `Last-Event-ID` 同列后续（v1 在 `OnError` / 流结束时 `OnCompleted`）。

解析逻辑置于运行时 `Observables.Sse.SseProtocol`（`public static`），R3 与 Reactive 两桥复用，避免重复实现。

## 5. 诊断（OBS8xxx）

| ID | 严重性 | 触发 | 归属 |
|----|--------|------|------|
| OBS8001 | Warning | `[Sse]` 接口成员无 `[SseEvent]` | 生成器 |
| OBS8002 | Error | 未引用 `Observables.Sse` | 生成器 |
| OBS8003 | Error | 不支持的返回类型 | 生成器 |
| OBS8004 | Error | `[SseEvent]` 标在方法上（须为属性） | 生成器 |
| OBS8005 | Error | `IObservable<T>` 需引用 `Observables.Sse.Reactive` | 生成器 |
| OBS8007 | Warning | `[Sse]` 接口为空 | Analyzer（`Observables.Analyzers`） |

`OBS8007` 复用 `EmptyProxyInterfaceAnalyzer`：在 `ProxyDomainCatalog` 登记 Sse 域 + `EmptySseInterface` 描述符即可。

## 6. 项目组成（克隆 WebSocket 模板）

```
Observables.Sse/
├── Observables.Sse/                          # 运行时（R3 桥 + SseConnection/SseProtocol）
├── Observables.Sse.Reactive/                 # System.Reactive 桥
├── Observables.Sse.SourceGenerators.Shared/  # shproj（新 GUID）+ Parser/Emitter/Models/诊断
├── Observables.Sse.R3.SourceGenerators/      # SSE_R3
├── Observables.Sse.Reactive.SourceGenerators/# SSE_REACTIVE
├── Observables.Sse.Package/                  # 产出 Observables.Sse.R3 / .Reactive 两包
├── Observables.Sse.R3.SourceGenerators.Tests/
├── Observables.Sse.Reactive.SourceGenerators.Tests/
├── Observables.Sse.Tests/                    # R3 E2E（内嵌 HttpListener SSE server）
└── Observables.Sse.Reactive.Tests/           # Reactive E2E
```

跨域登记：`eng/Observables.ProjectDefaults.props`（`/Observables.Sse/` 域文件夹）、`ProxyDomainCatalog`、`EmptyProxyInterfaceAnalyzer`、`Observables.slnx`（`/Sse/` 文件夹）、`eng/Observables.BuildManifest.json`（→ 14 包），以及 `eng/nuget-smoke/Sse.{R3,Reactive}.Consumer`。

## 7. 后续（v1 之外）

- 单连接多路复用（一条 `text/event-stream` 广播至多个事件属性）。
- 自动重连 + `Last-Event-ID` 续传。
- `retry:` 字段驱动退避。
- 自定义请求头 / 查询（可经 `SseConnection` 扩展或 `HttpClient.DefaultRequestHeaders`）。
