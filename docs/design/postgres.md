# 设计：Postgres 域

> 状态：主仓已实现（运行时 + 双路生成器 + Package + 三层测试）；**nuget.org 尚未发**第九域（本地 PackVerify / manifest 为 **18 包**，当前稳定版 `0.1.6` 仍为 **16 包**）。关联 Issue [#154](https://github.com/Skymly/Observables/issues/154) / [#160](https://github.com/Skymly/Observables/issues/160)。面向用户文档见 Observables.Docs（跨仓，未含在本票）。

## 1. 动机与定位

`Observables.Postgres` 将 **PostgreSQL LISTEN/NOTIFY**（[Npgsql](https://www.nuget.org/packages/Npgsql)）桥接为 interface-proxy IO 边界：

| 边界 | 方向 | PostgreSQL | 反应式映射 |
|------|------|------------|------------|
| **Listen** | server → client（流） | `LISTEN` + `Wait`/`Notification` | 热流 `Observable<T>` / `IObservable<T>` 属性 |
| **Notify** | client → server | `NOTIFY` / `pg_notify` | 冷流 `Observable<Unit>` / `IObservable<Unit>` 方法 |

工程骨架以 **Nats / Mqtt** 为模板（`[Postgres]` → `*GeneratedProxy` + `ModuleInitializer` → `PostgresService.For<T>`）。排名依据 [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)（LISTEN/NOTIFY 白名单 #1）。

## 2. 公共面

### 属性

| 属性 | 目标 | 说明 |
|------|------|------|
| `[Postgres]` | `interface` | 标记 LISTEN/NOTIFY 代理接口 |
| `[Listen(string? channel = null)]` | `property` | LISTEN 通道；热通知流；未指定时回退成员名 |
| `[Notify(string? channel = null)]` | `method` | NOTIFY 通道；冷发送流；未指定时回退成员名 |

通道名须为 **编译期字面量**（或成员名回退）；须匹配 PostgreSQL 标识符规则 `[A-Za-z_][A-Za-z0-9_]*`（最长 63）。**禁止** `{param}` 占位符与非法标识符（OBS10001 / OBS10006）。

### 运行时入口

```csharp
namespace Observables.Postgres;

public static class PostgresService
{
    public static T For<T>(NpgsqlConnection connection);
}
```

`For<T>` 接受已打开的 **专用** `NpgsqlConnection`。代理**不**释放该连接；连接生命周期应与 Listen 订阅 / `Wait` 循环对齐。

### 消费者示例

```csharp
[Postgres]
public interface IOrderHub
{
    [Listen("orders")]
    Observable<string> Orders { get; }

    [Notify("orders")]
    Observable<Unit> PublishOrder(string payload, CancellationToken cancellationToken = default);
}

// Dedicated Listen connection: Pooling=false + keepalive (see §4)
await using var connection = new NpgsqlConnection(
    "Host=…;Database=…;Username=…;Password=…;Pooling=false;Keepalive=30");
await connection.OpenAsync();
var hub = PostgresService.For<IOrderHub>(connection);
```

Reactive 路径：成员返回 `IObservable<T>`，引用 `Observables.Postgres.Reactive`；桥接为 `SystemReactivePostgresAdapter`。

## 3. 生成映射

- Listen 属性 → `PostgresObservable.FromListen` / `FromListen<T>`（或 Reactive 适配器同名方法）
- Notify 方法 → `PostgresObservable.FromNotify` / `FromNotify<T>`（空载荷、`string`、或序列化后的 `T`）

## 4. 不变量：专用连接与 keepalive

LISTEN 在会话上注册通道并占用连接上的 `Wait`/`WaitAsync` 循环，因此：

1. **专用、非池化连接**：不要把从 `NpgsqlDataSource` / 连接池借来的连接用于长生命周期 Listen。优先 `Pooling=false`（或显式工厂打开专用连接）。
2. **生命周期 = 订阅**：连接由调用方拥有；代理不 `Dispose` 连接。Listen 订阅结束时应 `UNLISTEN`（运行时尽力清理）并关闭/释放连接。
3. **勿与并发命令共享**：同一连接上不要与 Listen 的 `Wait` 循环并行跑其它查询。
4. **Keepalive（推荐）**：在专用 Listen 连接字符串上设置 Npgsql **`Keepalive`**（秒，例如 `Keepalive=30`），避免空闲 LISTEN 会话被中间设备或服务器超时断开。Notify 可用短生命周期连接或另一会话；不必与 Listen 共用同一连接。

`PostgresService.For` 的 XML 注释与本设计文档均陈述上述不变量。

## 5. Payload

| 类型 | 行为 |
|------|------|
| `string` | 通知载荷原文透传 |
| 其它 `T` | 默认 `System.Text.Json`（`JsonPostgresPayloadSerializer` / `PostgresPayload`） |
| 自定义 | `PostgresPayloadSerializers.Register<T>(…)`（委托、`IPostgresPayloadSerializer` / `IPostgresPayloadSerializer<T>`） |

原始原语路径见 `PrimitivePostgresPayloadSerializer`；trim/AOT 对 JSON 反射路径带 `RequiresUnreferencedCode` / `RequiresDynamicCode`。

## 6. 诊断（OBS10xxx）

| ID | 严重性 | 触发 |
|----|--------|------|
| OBS10001 | Warning | 缺少边界特性或 channel 非字面量 |
| OBS10002 | Error | 未引用 `Observables.Postgres` |
| OBS10003 | Error | 不支持的返回类型 |
| OBS10004 | Error | 成员形态与特性不匹配（如 `[Listen]` 在方法上） |
| OBS10005 | Error | `IObservable<T>` 需 `Observables.Postgres.Reactive` |
| OBS10006 | Error | 非法 channel / 占位符 / Notify 参数形态 |
| OBS10007 | Warning | 空 `[Postgres]` 接口（`EmptyProxyInterfaceAnalyzer`） |
| OBS10008 | Error | 生成器内部 fail-safe |

段分配权威见 `AGENTS.md` / `docs/ROADMAP.md`。OBS10007 在 Shared 分析器登记；其余在域 `DiagnosticDescriptors.cs`。

## 7. 项目组成

```
Observables.Postgres/
├── Observables.Postgres/
├── Observables.Postgres.Reactive/
├── Observables.Postgres.SourceGenerators.Shared/
├── Observables.Postgres.R3.SourceGenerators/
├── Observables.Postgres.Reactive.SourceGenerators/
├── Observables.Postgres.Package/
├── Observables.Postgres.R3.SourceGenerators.Tests/
├── Observables.Postgres.Reactive.SourceGenerators.Tests/
├── Observables.Postgres.Tests/
└── Observables.Postgres.Reactive.Tests/
```

登记：`Observables.slnx` `/Postgres/`、`eng/Observables.BuildManifest.json`（本地 **18** 包）、`ProxyDomainCatalog`、`ci.yml` postgres 矩阵、`eng/nuget-smoke`。

## 8. 测试

- 生成器 Verify + OBS10xxx 诊断断言（R3 + Reactive）
- E2E：B-tier 可移植 PostgreSQL 子进程（非 Docker 默认）；Listen 收跨会话 NOTIFY、Notify 被第二 LISTEN 观察到；typed JSON / 自定义序列化器往返
- nuget-smoke：`Postgres.{R3,Reactive}.Consumer`

## 9. 非目标（v1 显式排除）

- **逻辑复制**（logical replication）、replication slot、LSN、output plugin
- **结算类 API**：ack / offset / checkpoint / lease（ADR-002 白名单外）
- Request/Reply 模拟、第三反应式后端
- Redis / Diagnostic Source / RabbitMQ / AMQP 1.0（ADR-002 后续排名，另开 epic）
- Docker/Testcontainers 作为本域默认 E2E 依赖

## 10. Follow-up

- nuget.org 发第九域（版本须维护者批准；发版后触发 ADR-002 §6 mid-trigger 复审剩余 top-5）
- Observables.Docs `postgres.md` + `diagnostics.md` OBS10xxx；Observables.Samples.Postgres（跨仓）
- JSON sourcegen / 更严格 AOT 友好载荷路径

## 11. 参考

- 仓库内范本：**Nats**、**Mqtt**
- [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)
- [Npgsql — Notifications](https://www.npgsql.org/doc/notification.html)
