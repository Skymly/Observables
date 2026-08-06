# 设计：Diagnostics 域（Parked）

> 状态：**Parked（暂缓）** — 无运行时 / 生成器 / NuGet 排期。ADR-002 白名单 **#3** 仍保留名次，不自动实现。
> 关联：[ADR-002](../adr/ADR-002-domain-admission-and-ranking.md) §5；研究草图 [io-boundary-candidates.md §6.7](../research/io-boundary-candidates.md#67--net-诊断源eventsource--diagnosticlistener--activity--meter)。
> 本文记录 2026-08-06 grill 冻结的 **若 un-park 则采用的 v1 形状**；**不是**实现授权。

## 1. 为何 Park

BCL `DiagnosticListener` 已实现 `IObservable<KeyValuePair<string, object>>`。Observables 的增量是弱类型 → 强类型接口代理 + 双后端 `For<T>`，但维护者认定当前重叠成本高于收益，选择 **暂缓**，而非从排名表删除。

## 2. 动机与定位（若 un-park）

`Observables.Diagnostics` 将 **`DiagnosticListener` 事件**桥接为 interface-proxy IO 边界（仅 Subscribe）：

| 边界 | 方向 | BCL | 反应式映射 |
|------|------|-----|------------|
| **Subscribe** | listener → 消费者 | `DiagnosticListener.Subscribe` + `IsEnabled` | 热流 `Observable<T>` / `IObservable<T>` 属性 |

排名依据 [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)（.NET 诊断源白名单 #3）。

## 3. 冻结的 v1 决策（grill Q1–Q11）

| 主题 | 决策 |
|------|------|
| 机制范围 | **仅** `DiagnosticListener`；不做 `Activity` / `EventSource` / `Meter` |
| Feature stem / 包 | `Diagnostics` → `Observables.Diagnostics.R3` / `.Reactive` |
| 诊断段 | 预留 **`OBS12xxx`**（未登记实现） |
| 入口 | 仅 `DiagnosticService.For<T>(DiagnosticListener listener)`；不做 `AllListeners` |
| 写方向 | **不做** `DiagnosticSource.Write` / Publish 冷流 |
| 属性声明 | 接口 `[Diagnostics]`；成员 `[DiagnosticEvent("exact.name")]` **仅事件名**（listener 身份由 `For` 注入） |
| 事件名匹配 | **仅 Exact**；无 glob / prefix |
| Payload | **Cast**（`Value is T`）+ 可选 Envelope **`DiagnosticEvent<T>`**（`EventName` + `Value`）；不做 JSON deserialize |
| Cast 失败 | **一律 Skip**（含不匹配 / 不合适的 `null`） |
| `IsEnabled` | **Declared-only 并集**（接口上所有已声明事件名） |
| 订阅模型 | **每代理一次** `Subscribe` + 内部按事件名扇出 |

### 非目标（v1）

- ActivityListener / EventSource / Meter
- Write / Publish
- `DiagnosticListener.AllListeners` 全局入口
- 事件名 glob / regex
- JSON / 自定义 deserialize 路径
- 接口级强制期望 `listener.Name` 校验（可 follow-up）

### 示意（未实现）

```csharp
[Diagnostics]
public interface IHttpDiagnostics
{
    [DiagnosticEvent("System.Net.Http.HttpRequestOut.Start")]
    Observable<HttpRequestMessage> RequestStarted { get; }

    [DiagnosticEvent("System.Net.Http.HttpRequestOut.Stop")]
    Observable<DiagnosticEvent<object>> RequestStopped { get; } // Envelope 例；T 依真实 payload
}

// var proxy = DiagnosticService.For<IHttpDiagnostics>(listener);
```

## 4. Un-park 检查清单（将来）

1. 维护者明确批准实现与版本号
2. 按 AGENTS Feature 检查清单建运行时 + 双路生成器 + Package + 测试 + manifest
3. 登记 `OBS12xxx` + AnalyzerReleases
4. 三仓同步（Docs / Samples）
5. 视需要更新 ADR-002 #3 状态为已发版，并跑 §6 mid-trigger

## 5. 参考

- [ADR-002](../adr/ADR-002-domain-admission-and-ranking.md)
- [io-boundary-candidates.md §6.7](../research/io-boundary-candidates.md#67--net-诊断源eventsource--diagnosticlistener--activity--meter)
- [DiagnosticListener](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.diagnosticlistener)
