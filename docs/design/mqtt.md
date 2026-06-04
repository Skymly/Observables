# Mqtt Feature — 开发设计文档

> 状态：**已实现**（待合并 PR）；NuGet 将随下一预览发布（`Observables.Mqtt.R3` / `Observables.Mqtt.Reactive`）。对应 Issue [#50](https://github.com/Skymly/Observables/issues/50)。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 Mqtt 域。

## 1. 目标与定位

将 **MQTT 客户端**（建议以 [MQTTnet](https://github.com/dotnet/MQTTnet) 的 `IMqttClient` 为基线）的发布/订阅边界，通过 Roslyn 源生成器桥接为反应式流：

| 边界 | 方向 | 典型客户端 API | 对应已实现范本 |
|------|------|----------------|----------------|
| **发布 Publish** | 客户端 → Broker | `PublishAsync`（含 QoS / retain） | SignalR `[HubSend]` → `Observable<Unit>` |
| **订阅 Subscribe** | Broker → 客户端 | `ApplicationMessageReceived` / `SubscribeAsync` | SignalR `[HubOn]`、Events（热流、多播） |
| **请求-响应**（MQTT 5） | 双向 | `RequestAsync` / 关联 topic | RestAPI 单结果冷流（**首版可选 / follow-up**） |

最终发布 **两个 NuGet 包**（与 Events / RestAPI / SignalR 对齐）：

- `Observables.Mqtt.R3`
- `Observables.Mqtt.Reactive`

## 2. 现状（仓库内）

| 项 | 状态 |
|----|------|
| `Observables.Mqtt/` | **骨架**：`Observables.Mqtt`、`Observables.Mqtt.R3` 仅引用 `Observables.SourceGenerators.Shared`，**无** `.cs` 源、**无** `Package`、**无** Reactive 路 |
| slnx `/Mqtt/` | 仅上述两项目 |
| NuGet | **未发布** |
| 范本 | **SignalR**（接口代理 + 运行时桥接 + Shared 生成器 + 双包） |

实施时应 **对齐 SignalR 目录布局**（运行时、`*.Reactive`、`*.SourceGenerators.Shared`、双路生成器、`*.Package`、Tests），并视情况 **移除或替换** 旧版独立的 `Observables.Mqtt.R3` 生成器占位项目，避免与目标 `Observables.Mqtt.R3.SourceGenerators` 命名冲突。

## 3. API 规划

### 3.1 接口优先模型（主形态）

消费者声明 **主题代理接口**，用特性标注发布/订阅成员；`IMqttClient` 由调用方持有并传入工厂方法。

```csharp
using Observables.Mqtt;
using R3;

[Mqtt]
public interface ISensorHub
{
    // 订阅：Broker → 客户端（属性、无参；订阅时 SubscribeAsync + 消息桥接，热/多播）
    [MqttSubscribe("sensors/+/temperature")]
    Observable<TemperatureReading> Temperature { get; }

    // 发布：客户端 → Broker（方法；订阅时 PublishAsync，冷流发 Unit 后完成）
    [MqttPublish("commands/{deviceId}/restart")]
    Observable<Unit> Restart(string deviceId, CancellationToken cancellationToken = default);
}
```

获取实现：

```csharp
var client = new MqttFactory().CreateMqttClient();
await client.ConnectAsync(options);

ISensorHub hub = MqttService.For<ISensorHub>(client);

using var d = hub.Temperature.Subscribe(r => Console.WriteLine(r.Celsius));
await hub.Restart("device-42").FirstAsync();
```

设计要点：

- **入口对齐 SignalR / RestAPI**：`MqttService.For<T>(IMqttClient)` + `RegisterGeneratedFactory`（模块初始化器，无反射，AOT 友好）。
- **订阅**为**属性**（无参）；**发布**为**方法**（参数用于 topic 模板占位符与方法载荷）。形态不符报诊断（§5）。
- **Topic 模板**：`{param}` 段与方法参数名绑定；`+` / `#` 通配符按 MQTT 规则保留在模板字面量中；生成时拼出实际 topic 字符串。
- **载荷**：默认 JSON 反序列化为返回/参数类型（与 RestAPI body 类似）；首版可仅支持 `byte[]` / `ReadOnlyMemory<byte>` 与显式类型，JSON 为 follow-up 或需约定 `System.Text.Json` 依赖。
- **订阅流为多播热流**：首个订阅者 `SubscribeAsync` + 挂接 `ApplicationMessageReceived`（或 MQTTnet 推荐 API），末订阅者取消订阅并释放 handler（镜像 `SignalRObservable.FromOn` 引用计数）。

### 3.2 扩展模型（可选，次优先级）

对不声明接口、仅按 topic 字符串订阅的场景：

```csharp
Observable<TemperatureReading> stream =
    client.MqttTopics().Subscribe<TemperatureReading>("sensors/+/temperature");
```

首版可只做 §3.1，扩展在接口模型稳定后再评估。

### 3.3 特性集（运行时 `Observables.Mqtt`）

| 特性 | 目标 | 含义 |
|------|------|------|
| `[Mqtt]` | interface | 标记需生成代理的主题接口 |
| `[MqttPublish(template)]` | method | `PublishAsync`；返回 `Observable<Unit>`（或带 ACK 结果的 `Observable<MqttPublishResult>` — follow-up） |
| `[MqttSubscribe(template)]` | property | 订阅 topic；返回 `Observable<TPayload>`（热/多播） |

- `template` 省略时默认取成员名（需规范化，如 PascalCase → topic 片段策略在实现 Issue 中固定）。
- QoS、retain、content-type 等：首版可用 **可选特性参数** 或 **固定默认（QoS 0）**；复杂项列入 follow-up（OBS5006）。
- **不支持**首版：共享订阅 `$share/`、MQTT 5 用户属性全量映射、Will 消息生成（报诊断或文档非目标）。

## 4. 生成物形状

### 4.1 PostInitialization

与 SignalR 一致，发出与具体接口无关的基础设施：

- `MqttService`：`For<T>` + `RegisterGeneratedFactory`
- 桥接 helper（运行时 `Observables.Mqtt`）：`MqttObservable.FromPublish`、`FromSubscribe<T>`（命名待定，与 `SignalRObservable` 对称）

### 4.2 每接口产物

对每个 `[Mqtt]` 接口生成 `{Interface}GeneratedProxy`：

- 发布方法：解析 topic 模板 + 序列化载荷 → `PublishAsync`
- 订阅属性：`FromSubscribe<T>(client, resolvedTopic, qos, …)`

### 4.3 Reactive 后端

`Observables.Mqtt.Reactive.SourceGenerators` 同构代码：

- `Observable<T>` → `IObservable<T>`，`Unit` → `System.Reactive.Unit`
- `MqttObservable.*` → `Observables.Mqtt.Reactive.SystemReactiveMqttAdapter.*`

## 5. 诊断表（`OBS5xxx`）

按域分段（Events `OBS2xxx`、RestAPI `OBS3xxx`、SignalR `OBS4xxx`）。Category `Observables.Mqtt`，置于 `Observables.Mqtt.SourceGenerators.Shared`：

| ID | 标题 | 严重度 | 触发时机 |
|----|------|--------|----------|
| **OBS5001** | Mqtt interface members must declare a Mqtt boundary attribute | Warning | `[Mqtt]` 成员缺 `[MqttPublish]` / `[MqttSubscribe]`，或 topic 模板非字面量 |
| **OBS5002** | Observables.Mqtt must be referenced | Error | 发现 `[Mqtt]` 候选但未引用运行时 / MQTTnet |
| **OBS5003** | Unsupported member return type | Error | 返回类型不是 `Observable<T>` / `IObservable<T>`（Publish 须 `…<Unit>` 或约定类型） |
| **OBS5004** | Member shape mismatch for Mqtt boundary | Error | `[MqttSubscribe]` 用在方法、或 `[MqttPublish]` 用在属性等 |
| **OBS5005** | SystemReactive package required for IObservable | Error | Reactive 生成器遇 `IObservable<T>` 但未引用 `Observables.Mqtt.Reactive` |
| **OBS5006** | Unsupported Mqtt option or payload shape | Error | 首版不支持的 QoS/retain 组合、上行共享订阅、无效模板占位符等 |

## 6. 运行时 vs 仅分析器

**需要共享运行时**（同 RestAPI / SignalR）：

| 项目 | 内容 | 进哪个包 |
|------|------|----------|
| `Observables.Mqtt` | 特性、`MqttService`、`MqttObservable`（R3 桥接）、对 **MQTTnet** 的引用 | `Observables.Mqtt.R3`（`lib/`） |
| `Observables.Mqtt.Reactive` | `SystemReactiveMqttAdapter` | `Observables.Mqtt.Reactive`（`lib/`） |

- `FromPublish`：冷流，`PublishAsync` 完成后发 `Unit`（或错误终止流）。
- `FromSubscribe`：热流，引用计数管理订阅生命周期与 handler 注销。
- **MQTTnet** 作为运行时包 **public 依赖**（消费者传入 `IMqttClient`）。

## 7. slnx / 项目清单（目标）

物理目录 `Observables.Mqtt/`，slnx `/Mqtt/`（含 `Tests` 子夹）。

| 项目 | 现状 | 说明 |
|------|------|------|
| `Observables.Mqtt` | 骨架 | 特性、`MqttService`、`MqttObservable` |
| `Observables.Mqtt.Reactive` | 未建 | System.Reactive 桥接 |
| `Observables.Mqtt.SourceGenerators.Shared` | 未建 | Parser、Emitter、OBS5xxx |
| `Observables.Mqtt.R3.SourceGenerators` | 未建（旧 `Observables.Mqtt.R3` 占位待替换） | R3 生成器 |
| `Observables.Mqtt.Reactive.SourceGenerators` | 未建 | Reactive 生成器 |
| `Observables.Mqtt.Package` | 未建 | `Observables.Mqtt.R3` / `.Reactive` |
| `Observables.Mqtt.R3.SourceGenerators.Tests` | 未建 | Verify 快照 |
| `Observables.Mqtt.Reactive.SourceGenerators.Tests` | 未建 | 建议与 SignalR 对齐 |
| `Observables.Mqtt.Tests` | 未建 | 可选：嵌入式 broker / MQTTnet 测试 host |

配套（实现阶段）：

- Nuke `PackVerify` + `eng/nuget-smoke` 消费者
- `Observables.Docs` 中英文 `mqtt.md` + OBS5xxx
- `Observables.Samples.Mqtt`（可选；CI 可用 mock / 无 broker 注册演示）

## 8. 建议实施链（follow-up Issues，逐个 PR）

1. **Shared 模型 + 运行时**：特性、`MqttService`、`MqttObservable`、`SourceGenerators.Shared`（OBS5xxx）；整理 slnx，移除旧占位冲突。
2. **R3 生成器** + Verify 测试。
3. **Reactive 生成器** + Reactive 测试。
4. **打包**：`Observables.Mqtt.Package`、Nuke、nuget-smoke。
5. **测试加固**：topic 模板、通配符、断开重连（若运行时覆盖）。
6. **Samples / Docs**（可独立 PR）。

## 9. 非目标

- 设计期**不**改 `PackageVersion`、不打 tag、不发 NuGet。
- **不**与 Events / RestAPI / SignalR 混 PR。
- 首版**不**实现 Broker 服务端、**不**强制 MQTT 5 请求-响应（可 follow-up）。
- **不**在 design PR 中实现 WebSocket / Grpc。

## 10. 参考

- 仓库内范本：**SignalR**（`HubService`、`SignalRObservable`、OBS4xxx、Package 形状）、**RestAPI**（冷流单元素）、**Events**（热流订阅）。
- 外部：`MQTTnet` 文档（`IMqttClient.ConnectAsync` / `PublishAsync` / `SubscribeAsync` / 消息事件）。
- RefReps：[`C:\RefReps\Docs\messaging-rpc.md`](C:\RefReps\Docs\messaging-rpc.md)（进程内 MessagePipe vs 跨进程 RPC；MQTT 属 Broker 消息，非 gRPC）。
