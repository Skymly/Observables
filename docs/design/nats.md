# 设计：NATS 域

> 状态：M6 已发 **`0.1.0-preview8`**。面向用户文档见 Observables.Docs `nats.md`。

## 1. 动机与定位

`Observables.Nats` 引入 **Core NATS** 客户端边界（[NATS.Client.Core](https://www.nuget.org/packages/NATS.Client.Core)）：

| 边界 | 方向 | NATS API | 反应式映射 |
|------|------|----------|------------|
| **Subscribe** | server → client（流） | `SubscribeAsync` | 热流 `Observable<T>` 属性 |
| **Publish** | client → server | `PublishAsync` | 冷流 `Observable<Unit>` 方法 |
| **Request** | 请求-响应 | `RequestAsync` | 冷流单值 `Observable<TResponse>` 方法 |

工程骨架以 **Mqtt 域**为模板（interface-proxy：`[Nats]` → `*GeneratedProxy` + `ModuleInitializer`）。

**v1 非目标**：JetStream 持久化消费（见 §9 Follow-up）。

## 2. 公共面

### 属性

| 属性 | 目标 | 说明 |
|------|------|------|
| `[Nats]` | `interface` | 标记 subject 代理接口 |
| `[NatsSubscribe(string? subject = null)]` | `property` | 订阅 subject（支持 `*` / `>` 通配符；**禁止** `{param}` 占位符） |
| `[NatsPublish(string? subjectTemplate = null)]` | `method` | 发布；subject 模板支持 `{param}` |
| `[NatsRequest(string? subjectTemplate = null)]` | `method` | 请求-响应；首参或显式 request 参数序列化为 payload |

### 运行时入口

```csharp
namespace Observables.Nats;

public static class NatsService
{
    public static T For<T>(INatsConnection connection);
}
```

### 消费者示例

```csharp
[Nats]
public interface IOrderHub
{
    [NatsSubscribe("orders.>")]
    Observable<OrderEvent> OrderEvents { get; }

    [NatsPublish("orders.{id}.cancel")]
    Observable<Unit> Cancel(string id, CancellationToken cancellationToken = default);

    [NatsRequest("orders.validate")]
    Observable<ValidationResult> Validate(OrderRequest request, CancellationToken cancellationToken = default);
}

await using var nats = new NatsConnection(new NatsOpts { Url = "nats://127.0.0.1:4222" });
var hub = NatsService.For<IOrderHub>(nats);
```

## 3. Subject 规则

- 分隔符 `.`；通配符 `*`（单 token）、`>`（尾部多 token，仅末尾）
- Subscribe：**字面量** subject/filter，禁止 `{param}`（OBS9006）
- Publish/Request：`{param}` 绑定方法参数名（`NatsSubject.Format`）
- 未指定 subject 时回退为成员名

## 4. 生成映射

- Subscribe 属性 → `NatsObservable.FromSubscribe<T>(connection, subject)`
- Publish 方法 → `NatsObservable.FromPublish(connection, subject, args...)`
- Request 方法 → `NatsObservable.FromRequest<TReq,TRes>(connection, subject, request, ct)`

Reactive 桥：`SystemReactiveNatsAdapter` 同名方法，返回 `IObservable<T>`。

## 5. Payload

`NatsPayloadSerializers` 镜像 Mqtt：`string`/`byte[]` 原始；net8+ 其余类型 STJ JSON；可注册自定义序列化器。

## 6. 诊断（OBS9xxx）

| ID | 严重性 | 触发 |
|----|--------|------|
| OBS9001 | Warning | 缺少边界特性或 subject 非字面量 |
| OBS9002 | Error | 未引用 `Observables.Nats` |
| OBS9003 | Error | 不支持的返回类型 |
| OBS9004 | Error | 成员形态与特性不匹配 |
| OBS9005 | Error | `IObservable<T>` 需 `Observables.Nats.Reactive` |
| OBS9006 | Error | subject 模板 / subscribe 占位符违规 |
| OBS9007 | Warning | 空 `[Nats]` 接口（Analyzer） |

## 7. 项目组成

```
Observables.Nats/
├── Observables.Nats/
├── Observables.Nats.Reactive/
├── Observables.Nats.SourceGenerators.Shared/
├── Observables.Nats.R3.SourceGenerators/
├── Observables.Nats.Reactive.SourceGenerators/
├── Observables.Nats.Package/
├── Observables.Nats.R3.SourceGenerators.Tests/
├── Observables.Nats.Reactive.SourceGenerators.Tests/
├── Observables.Nats.Tests/
└── Observables.Nats.Reactive.Tests/
```

登记：`Observables.slnx`、`Observables.BuildManifest.json`（16 包）、`ProxyDomainCatalog`、`ci.yml` nats 矩阵、`eng/nuget-smoke`。

## 8. 测试

- 生成器 Verify + OBS9004/9005 诊断测试
- E2E：`NATS.Client.TestUtilities` 进程内 server；Subscribe 收消息、Publish 到达、Request 往返
- R3 + Reactive 各一套

## 9. Follow-up（非 v1）

- **JetStream**：持久化 stream / ordered consumer → `Observable<T>`
- 重连与 inbox 策略
- JSON sourcegen / trim 友好 AOT
- 多参数 Publish payload 编码策略文档化

## 10. 参考

- 仓库内范本：**Mqtt**、**SignalR**
- [nats-io/nats.net](https://github.com/nats-io/nats.net)
