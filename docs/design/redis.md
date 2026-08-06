# 设计：Redis 域

> 状态：已随 **`0.1.9`** 发至 nuget.org（**20 包**）。关联 PRD [#169](https://github.com/Skymly/Observables/issues/169)（库内切片 #170–#177）。面向用户文档：Observables.Docs（[#14](https://github.com/Skymly/Observables.Docs/issues/14) / PR [#15](https://github.com/Skymly/Observables.Docs/pull/15)）；Samples：[#11](https://github.com/Skymly/Observables.Samples/issues/11) / PR [#12](https://github.com/Skymly/Observables.Samples/pull/12)。

## 1. 动机与定位

`Observables.Redis` 将 **经典 Redis Pub/Sub**（[StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis)）桥接为 interface-proxy IO 边界：

| 边界 | 方向 | Redis 命令 | 反应式映射 |
|------|------|------------|------------|
| **Subscribe** | server → client（流） | `SUBSCRIBE` / `PSUBSCRIBE` | 热流 `Observable<T>` / `IObservable<T>` 属性 |
| **Publish** | client → server | `PUBLISH` | 冷流 `Observable<Unit>` / `IObservable<Unit>` 方法 |

工程骨架以 **Nats** 为模板（Subscribe 属性 + Publish 方法；**无** Request 边界）。排名依据 [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)（Redis Pub/Sub 白名单 #2）。

## 2. 公共面

### 属性

| 属性 | 目标 | 说明 |
|------|------|------|
| `[Redis]` | `interface` | 标记 Pub/Sub 代理接口 |
| `[RedisSubscribe(string? channel = null)]` | `property` | 订阅 Channel / Pattern；热流；未指定时回退成员名 |
| `[RedisPublish(string? channelTemplate = null)]` | `method` | 发布；Channel 模板支持 `{param}`；未指定时回退成员名 |

### 运行时入口

```csharp
namespace Observables.Redis;

public static class RedisService
{
    public static T For<T>(IConnectionMultiplexer multiplexer);
}
```

代理内部经 `multiplexer.GetSubscriber()` 获取 `ISubscriber`；**不**释放调用方传入的 multiplexer。

### 消费者示例

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

Reactive 路径：成员返回 `IObservable<T>`，引用 `Observables.Redis.Reactive`；桥接为 `SystemReactiveRedisAdapter`。

## 3. Channel / Pattern 规则

- **Subscribe**：字面量 Channel 或 Pattern。若字符串含 `*` 或 `?` → `PSUBSCRIBE`（Pattern）；否则 → `SUBSCRIBE`（exact）。**禁止** `{param}` 占位符（OBS11006）。
- **Publish**：exact Channel only；模板支持 `{param}` 绑定方法参数名（`RedisChannelTemplate`）。**禁止** Pattern 元字符 `*` / `?`（OBS11006）。可选末尾 `CancellationToken`。
- 未指定 Channel / 模板时回退为成员名（与 Nats/Mqtt 同惯例）。

## 4. 流元素类型

| 返回类型 | 语义 |
|----------|------|
| `Observable<T>` / `IObservable<T>` | 仅载荷（payload-only） |
| `Observable<RedisMessage<T>>` / `IObservable<RedisMessage<T>>` | 信封：具体 Channel + 载荷（Pattern 场景可按 Channel 分支） |

`RedisMessage<T>` 位于 `Observables.Redis`，避免与 StackExchange.Redis `ChannelMessage` 命名冲突。

## 5. 生成映射

- Subscribe 属性 → `RedisObservable.FromSubscribe` / `FromPatternSubscribe`（或 `*Message` 信封变体）
- Publish 方法 → `RedisObservable.FromPublish`（含模板格式化与序列化）

Reactive：`SystemReactiveRedisAdapter` 同名方法，返回 `IObservable<T>`。

## 6. Payload

| 类型 | 行为 |
|------|------|
| `string` / `byte[]`（及约定的原始等价类型） | 透传，不经 JSON |
| 其它 `T` | 默认 `System.Text.Json`（支持 TFM） |
| 自定义 | `RedisPayloadSerializers.Register<T>(…)` |

trim/AOT：JSON 反射路径带 `RequiresUnreferencedCode` / `RequiresDynamicCode`（与兄弟 IO 域一致）。

## 7. 分发

v1 **固定顺序** Subscribe 投递（StackExchange.Redis `ChannelMessageQueue` / `OnMessage` 顺序路径）；**无**并发开关。

Dispose 订阅 → 取消 Redis 订阅，避免泄漏。

## 8. 诊断（OBS11xxx）

| ID | 严重性 | 触发 |
|----|--------|------|
| OBS11001 | Warning | 缺少边界特性或 Channel 非字面量 |
| OBS11002 | Error | 未引用 `Observables.Redis` |
| OBS11003 | Error | 不支持的返回类型 |
| OBS11004 | Error | 成员形态与特性不匹配（如 `[RedisSubscribe]` 在方法上） |
| OBS11005 | Error | `IObservable<T>` 需 `Observables.Redis.Reactive` |
| OBS11006 | Error | Channel/模板违规（Subscribe `{param}`、Publish glob、参数形态等） |
| OBS11007 | Warning | 空 `[Redis]` 接口（`EmptyProxyInterfaceAnalyzer`） |
| OBS11008 | Error | 生成器内部 fail-safe |

段分配权威见 `AGENTS.md` / `docs/ROADMAP.md`。OBS11007 在 Shared 分析器登记；其余在域 `DiagnosticDescriptors.cs`。OBS0001 将 Redis R3/Reactive 与其它域一样纳入冲突检测。

## 9. 项目组成

```
Observables.Redis/
├── Observables.Redis/
├── Observables.Redis.Reactive/
├── Observables.Redis.SourceGenerators.Shared/
├── Observables.Redis.R3.SourceGenerators/
├── Observables.Redis.Reactive.SourceGenerators/
├── Observables.Redis.Package/
├── Observables.Redis.R3.SourceGenerators.Tests/
├── Observables.Redis.Reactive.SourceGenerators.Tests/
├── Observables.Redis.Tests/
└── Observables.Redis.Reactive.Tests/
```

登记：`Observables.slnx` `/Redis/`、`ProxyDomainCatalog`、`eng/Observables.BuildManifest.json`（**20** 包）、`ci.yml` redis 矩阵、`eng/nuget-smoke`。

## 10. 测试

- **生成器**：R3 + Reactive Verify / 诊断断言（OBS11xxx）
- **E2E**：进程内 **Microsoft Garnet**（spike #170 通过后锁定）；覆盖 exact Channel、Pattern、payload-only vs `RedisMessage<T>`、Publish `{param}`、顺序投递
- **nuget-smoke**：`Redis.{R3,Reactive}.Consumer`（#176）
- **PackVerify**：analyzers + `lib/`；断言 Garnet **不**进入 pack 依赖图（仅测试项目引用 `Microsoft.Garnet`）

## 11. 非目标（v1 显式排除）

- Redis **Streams**、consumer groups、ack/cursor/lease
- Keyspace notifications
- Request-reply（含临时 reply-Channel RPC）
- Cluster **sharded** Pub/Sub（`SPUBLISH` / `SSUBSCRIBE` 等）
- 并发 Subscribe 分发开关
- 第三反应式后端；R3 包引用 System.Reactive 或反向（ADR-001）

## 12. Follow-up

- ✅ `v0.1.9` 已发
- ✅ Observables.Docs Redis 页 + `diagnostics.md` OBS11xxx（[#14](https://github.com/Skymly/Observables.Docs/issues/14) / PR [#15](https://github.com/Skymly/Observables.Docs/pull/15)）
- ✅ Observables.Samples.Redis（[#11](https://github.com/Skymly/Observables.Samples/issues/11) / PR [#12](https://github.com/Skymly/Observables.Samples/pull/12)）
- JSON sourcegen / 更严格 AOT 友好载荷路径（可选）

## 13. 参考

- 仓库内范本：**Nats**、**Mqtt**
- [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)
- Spike：[`eng/spikes/GarnetPubSub`](../../eng/spikes/GarnetPubSub/README.md)
- [StackExchange.Redis Pub/Sub order](https://stackexchange.github.io/StackExchange.Redis/PubSubOrder.html)
- [Redis Pub/Sub](https://redis.io/docs/latest/develop/pubsub/)
