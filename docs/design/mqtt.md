# Mqtt Feature — 开发设计文档

> 状态：**已实现**（`main`）；NuGet 将随下一预览发布（`Observables.Mqtt.R3` / `Observables.Mqtt.Reactive`）。对应 Issue [#50](https://github.com/Skymly/Observables/issues/50)（已关闭）。实现细节以代码为准。
> 命名、打包、诊断分段等约定以仓库根 [`AGENTS.md`](../../AGENTS.md) 为权威，本文在其框架内细化 Mqtt 域。

## 1. 目标与定位

将 **MQTT 客户端**（[MQTTnet](https://github.com/dotnet/MQTTnet) `IMqttClient`）的发布/订阅边界，通过 Roslyn 源生成器桥接为反应式流：

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
| NuGet | **未发布**（`0.1.0-preview3` 仍为 6 包；CiPack **8 包** 含 Mqtt） |
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
var client = new MqttFactory().CreateMqttClient();
await client.ConnectAsync(options);

ISensorHub hub = MqttService.For<ISensorHub>(client);
```

设计要点：

- **入口对齐 SignalR / RestAPI**：`MqttService.For<T>(IMqttClient)` + `RegisterGeneratedFactory`（模块初始化器，无反射，AOT 友好）。
- **订阅**为**属性**（无参）；**发布**为**方法**（参数用于 topic 模板占位符）。订阅 topic **不支持** `{param}` 占位符（OBS5006）。
- **Topic 模板**：`{param}` 段与方法参数名绑定；`+` / `#` 通配符保留在模板字面量中。
- **载荷**：首版以 UTF-8 字符串 / 空 payload 为主；JSON 反序列化见运行时 `MqttObservable`（带 trim/AOT 警告）。

### 3.2 扩展模型（可选，次优先级）

对不声明接口、仅按 topic 字符串订阅的场景：`MqttObservable.FromSubscribe` / `FromPublish`（见运行时）。

## 4. 诊断（OBS5xxx）

| ID | 级别 | 场景 |
|----|------|------|
| OBS5001 | Warning | 缺少边界特性或非常量 topic |
| OBS5002 | Error | 未引用 Observables.Mqtt 运行时 |
| OBS5003 | Error | 不支持的返回类型 |
| OBS5004 | Error | 成员形态与特性不匹配 |
| OBS5005 | Error | `IObservable<T>` 未引用 Reactive 包 |
| OBS5006 | Error | 不支持的 topic 模板、多余参数或订阅占位符 |

用户向说明：[Observables.Docs diagnostics](https://github.com/Skymly/Observables.Docs/blob/main/docs/diagnostics.md)。

## 5. 目录与打包（已实现）

对齐 SignalR：`Observables.Mqtt`、`Observables.Mqtt.Reactive`、`SourceGenerators.Shared`、双路生成器、`Observables.Mqtt.Package`、Nuke PackVerify + `eng/nuget-smoke`。

## 6. 测试（已实现）

| 项目 | 说明 |
|------|------|
| `*.R3.SourceGenerators.Tests` / `*.Reactive.SourceGenerators.Tests` | Verify 快照 |
| `Observables.Mqtt.Tests` / `Observables.Mqtt.Reactive.Tests` | 进程内 `MqttTestBroker`（MQTTnet），Publish/Subscribe E2E |

## 7. 建议实施链

1. ~~Shared 模型 + 运行时~~ ✅
2. ~~R3 生成器 + 测试~~ ✅
3. ~~Reactive 生成器 + 测试~~ ✅
4. ~~打包 + nuget-smoke~~ ✅
5. ~~进程内 Broker E2E~~ ✅（PR [#54](https://github.com/Skymly/Observables/pull/54)）
6. ~~用户文档（Observables.Docs）~~ ✅
7. ~~Samples.Mqtt~~ ✅（Observables.Samples）
8. **follow-up**：MQTT 5 请求-响应、断开重连策略、JSON sourcegen for AOT

## 8. 非目标

- 设计/收尾波次**不**改 `PackageVersion`、不打 tag、不发 NuGet（除非维护者指定版本）。
- **不**与 Events / RestAPI / SignalR 混 PR。
- 首版**不**实现 Broker 服务端。

## 9. 参考

- 仓库内范本：**SignalR**、**RestAPI**、**Events**
- 外部：`MQTTnet` 文档
- RefReps：[`C:\RefReps\Docs\messaging-rpc.md`](C:\RefReps\Docs\messaging-rpc.md)
