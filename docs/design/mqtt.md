# Mqtt Feature — 开发设计文档

> 状态：**已实现**（`main`）；NuGet 将随下一预览发布（`Observables.Mqtt.R3` / `Observables.Mqtt.Reactive`）。对应 Issue [#50](https://github.com/Skymly/Observables/issues/50)（已关闭）。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 Mqtt 域。

## 1. 目标与定位

将 **MQTT 传输接口**（`IMqttTransport`）的发布/订阅边界，通过 Roslyn 源生成器桥接为反应式流：

| 边界 | 方向 | 典型客户端 API | 对应已实现范本 |
|------|------|----------------|----------------|
| **发布 Publish** | 客户端 → Broker | `PublishAsync`（含 QoS / retain） | SignalR `[HubSend]` → `Observable<Unit>` |
| **订阅 Subscribe** | Broker → 客户端 | `ApplicationMessageReceived` / `SubscribeAsync` | SignalR `[HubOn]`、Events（热流、多播） |
| **请求-响应**（MQTT 5） | 双向 | `RequestAsync` / 关联 topic | RestAPI 单结果冷流（**首版未实现 / follow-up**） |

发布 **两个 NuGet 包**（与 Events / RestAPI / SignalR 对齐）：

- `Observables.Mqtt.R3`
- `Observables.Mqtt.Reactive`

## 2. 现状（仓库内）

| 项 | 状态 |
|----|------|
| `Observables.Mqtt/` | **已实现**：运行时、双路生成器、Package、生成器测试、**进程内 Broker E2E**（`Mqtt.Tests` / `Mqtt.Reactive.Tests`） |
| slnx `/Mqtt/` | 运行时、生成器、Package、Tests |
| NuGet | **`0.1.0-preview4`**（8 包，含 Mqtt） |
| 用户文档 | [Observables.Docs](https://github.com/Skymly/Observables.Docs) `mqtt.md` / `zh/mqtt.md` |
| 示例 | [Observables.Samples.Mqtt](https://github.com/Skymly/Observables.Samples)（需 `UseLocalObservables=true` 直至下一预览 NuGet） |

## 3. API 规划

### 3.1 接口优先模型（主形态）

消费者声明 **主题代理接口**，用特性标注发布/订阅成员；`IMqttClient` 由调用方持有并传入工厂方法。

```csharp
using Observables.Mqtt;
using R3;

[Mqtt]
public interface ISensorHub
{
    [MqttSubscribe("sensors/+/temperature")]
    Observable<TemperatureReading> Temperature { get; }

    [MqttPublish("commands/{deviceId}/restart")]
    Observable<Unit> Restart(string deviceId, CancellationToken cancellationToken = default);
}
```

获取实现：

```csharp
using var client = new MqttFactory().CreateMqttClient();
await client.ConnectAsync(options);

ISensorHub hub = MqttService.For<ISensorHub>(MqttNetTransport.Wrap(client));
```

设计要点：

- **入口对齐 SignalR / RestAPI**：`MqttService.For<T>(IMqttTransport)` + `RegisterGeneratedFactory`（模块初始化器，无反射，AOT 友好）。
- **具名客户端**：`[Mqtt(clientName)]` 上的名字由 Parser 读出、随 `RegisterProxyName(Type, string)` 一起发到模块初始化器（生成物 `MqttProxyNames.g.cs`），`MqttService.For<T>()` 这个不带客户端参数的重载据此查 `RegisterClient(name, client)` 注册的客户端。名字走生成代码而不是运行期读特性，是因为域运行时要对 trim / AOT 分析器零告警。未写名字的接口仍只能走 `For<T>(IMqttTransport)`；解析未命名接口、或名字没注册过客户端，都抛 `InvalidOperationException` 并在消息里点名缺的是哪一个。
- **订阅**用只读属性声明 MQTT 主题过滤器，支持标准 `+` / `#` 通配符和空层级。带 `{param}` 或以 `$share/` 开头的过滤器会报 OBS5006。
- **发布**用方法声明 topic 模板。`{param}` 与方法参数名绑定，每个普通参数都必须被引用，同一个占位符可以重复出现。发布 topic 使用普通主题名，`+` 和 `#` 会报 OBS5006。
- **载荷**：`MqttPayloadSerializers`（默认 `DefaultMqttPayloadSerializer`：`string`/`byte[]` 原始 UTF-8，net8+ 其余类型走 STJ JSON）。可 `Register<T>` 或替换 `Current`（`IMqttPayloadSerializer` / `IMqttPayloadSerializer<T>`）；netstandard2.0 仅原始类型。

### 3.2 扩展模型（可选，次优先级）

对不声明接口、仅按 topic 字符串订阅的场景：`MqttObservable.FromSubscribe` / `FromPublish`（见运行时）。

### 3.3 传输接口与 MQTTnet 适配

按 [ADR-004](../adr/ADR-004-mqtt-transport-adapters.md)，`IMqttTransport` 提供订阅、退订、发布和消息回调。MQTTnet 4 / 5 适配器分别实现接口，由应用选择一套。代理与桥接共用中立运行时。

从 0.4.x 升级时，按 MQTTnet 主版本安装 `Observables.Mqtt.MQTTnet4` 或 `Observables.Mqtt.MQTTnet5`，并导入同名命名空间。把 `MqttNetTransport.Wrap(client)` 传给 `MqttService.For<T>`、`RegisterClient` 或直接调用的桥接方法。MQTTnet 4 适配器保留 `netstandard2.0`，MQTTnet 5 适配器要求 .NET 8 或更高。应用负责连接、选择协议版本及处置客户端。

`MqttProtocol` 按传输实例和主题过滤器计数。首次订阅向 broker 注册过滤器，最后一次取消时退订。`Wrap` 为同一 MQTTnet 客户端返回同一个传输实例。包装和取消订阅不会断开 MQTTnet 客户端。

## 4. 诊断（OBS5xxx）

| ID | 级别 | 场景 |
|----|------|------|
| OBS5001 | Warning | 缺少边界特性或非常量 topic |
| OBS5002 | Error | 未引用消费者包 Observables.Mqtt.R3 / Observables.Mqtt.Reactive（按生成器后端） |
| OBS5003 | Error | 不支持的返回类型 |
| OBS5004 | Error | 成员形态与特性不匹配 |
| OBS5005 | Error | `IObservable<T>` 未引用 Reactive 包 |
| OBS5006 | Error | 非法 topic 或过滤器、多余参数、订阅占位符或不受支持的 `$share/` 过滤器 |
| OBS5007 | Warning | 空 `[Mqtt]` 接口（`Observables.Analyzers`） |
| OBS5008 | Error | 生成器内部错误 |

用户向说明：[Observables.Docs diagnostics](https://github.com/Skymly/Observables.Docs/blob/main/docs/diagnostics.md)。

## 5. 目录与打包（已实现）

对齐 SignalR：`Observables.Mqtt`、`Observables.Mqtt.R3`、`Observables.Mqtt.Reactive`、`SourceGenerators.Shared`、双路生成器、`Observables.Mqtt.Package`、Nuke PackVerify + `eng/nuget-smoke`。

包布局见 [ADR-004](../adr/ADR-004-mqtt-transport-adapters.md)。

| 程序集 | 依赖 | 进哪个包 |
|--------|------|----------|
| `Observables.Mqtt` | ns2.0 使用 System.Memory | `Observables.Mqtt` 中立运行时包 |
| `Observables.Mqtt.R3`（`MqttObservable`） | 上面那个 + R3 | 仅 `Observables.Mqtt.R3` |
| `Observables.Mqtt.Reactive`（`SystemReactiveMqttAdapter`） | 上面第一个 + System.Reactive | 仅 `Observables.Mqtt.Reactive` |
| `Observables.Mqtt.MQTTnet4` / `.MQTTnet5` | 中立运行时 + 各自 MQTTnet 主版本 | 同名的适配包 |

使用 `MqttService` 和 R3 的 `MqttObservable` 时导入 `using Observables.Mqtt;`。直接调用 System.Reactive 桥 `SystemReactiveMqttAdapter` 时导入 `using Observables.Mqtt.Reactive;`。`MqttProtocol` 等内部类型由两个桥接项目通过 `InternalsVisibleTo` 共享。

## 6. 测试（已实现）

| 项目 | 说明 |
|------|------|
| `*.R3.SourceGenerators.Tests` / `*.Reactive.SourceGenerators.Tests` | Verify 快照 |
| `Observables.Mqtt.Tests` / `Observables.Mqtt.Reactive.Tests` | 进程内 MQTTnet 4 broker 的发布、订阅与退订测试 |
| `Observables.Mqtt.MQTTnet5.Tests` | 进程内 MQTTnet 5 broker 的发布、订阅与退订测试 |
| `eng/nuget-smoke` | 两种 MQTTnet 适配包与两种反应式包的四种组合 |

## 7. 建议实施链

1. ~~Shared 模型 + 运行时~~ ✅
2. ~~R3 生成器 + 测试~~ ✅
3. ~~Reactive 生成器 + 测试~~ ✅
4. ~~打包 + nuget-smoke~~ ✅
5. ~~进程内 Broker E2E~~ ✅（PR [#54](https://github.com/Skymly/Observables/pull/54)）
6. ~~用户文档（Observables.Docs）~~ ✅
7. ~~Samples.Mqtt~~ ✅（Observables.Samples）
8. .NET 8 或更高版本可用 `MqttPayloadSerializers.RegisterJson(MyJsonContext.Default.Reading)` 注册生成的 JSON 元数据。普通代理订阅使用同一注册表。关闭反射时，原始字符串和字节数组仍直接处理，未注册的复杂载荷会报错。
9. **后续**：MQTT 5 请求-响应、断开重连策略。

## 8. 非目标

- 设计/收尾波次**不**改 `PackageVersion`、不打 tag、不发 NuGet（除非维护者指定版本）。
- **不**与 Events / RestAPI / SignalR 混 PR。
- 首版**不**实现 Broker 服务端。

## 9. 参考

- 仓库内范本：**SignalR**、**RestAPI**、**Events**
- 外部：`MQTTnet` 文档
- RefReps：[`C:\RefReps\Docs\messaging-rpc.md`](C:\RefReps\Docs\messaging-rpc.md)
