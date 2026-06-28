# ADR-0001: 不采用 ReactiveUI.Primitives 作为第三后端

- **状态**: 已决定（SKIP）
- **日期**: 2026-06-28
- **决策者**: 维护者
- **评估方式**: 4 路并行子代理调研（API 兼容性 / 架构影响 / 价值与风险 / 竞争定位）

## 背景

Observables 当前支持两种反应式后端：

| 后端 | 包 ID 模式 | 运行时依赖 |
|------|-----------|-----------|
| **R3** | `Observables.<Feature>.R3` | `R3`（Cysharp，高性能，自创 `Observable<T>` 类型） |
| **System.Reactive** | `Observables.<Feature>.Reactive` | `System.Reactive`（成熟，`IObservable<T>` 生态） |

ReactiveUI.Primitives（github.com/reactiveui/Primitives）是一个零运行时依赖的反应式原语库，v5.7.0，MIT 许可。其定位为"不依赖 System.Reactive 或 R3 的高性能反应式原语"。

本 ADR 记录是否应将 Primitives 作为第三种后端集成到 Observables 的 8 域源生成器中。

## 评估

### 1. API 兼容性

生成器当前依赖的核心 API 在 Primitives 中大面积缺失：

| 需要 | R3 | System.Reactive | Primitives | 状态 |
|------|:---:|:---:|:---:|:---:|
| `Observable.FromAsync` | ✅ | ✅ (adapter) | ❌ | GAP |
| `Observable.Create` | ✅ | ✅ | ❌ | GAP |
| `Observable.FromEvent` | ✅ | ✅ | ❌ | GAP |
| `Observable.Return/Empty/Never` | ✅ | ✅ | ❌ | GAP |
| `Subject<T>` | ✅ | ✅ | `Signal<T>` | 改名可用 |
| `BehaviorSubject<T>` | ✅ | ✅ | `BehaviorSignal<T>` | 改名可用 |
| `ReplaySubject<T>` | ✅ | ✅ | ❌ | GAP |
| `Unit` 类型 | ✅ | ✅ | ❌ | GAP |

Primitives 是"构建块库"而非"完整 Rx 框架"。生成器需要 `Observable.FromAsync`/`FromEvent`/`Create` 等静态工厂，Primitives 不提供。每域都需写一个完整桥接适配器，工作量等于重做一遍 System.Reactive 后端。

### 2. 架构影响

三种集成方案对比：

| 方案 | 新增项目 | CI 膨胀 | shproj 改动 | 工期 |
|------|:---:|:---:|:---:|:---:|
| A: 朴素三后端 | +32 | +50% | 100+ 处 3-way `#if` | 16+ 天 |
| B: BCL 后端 | +32 | +50% | 0（但生成器逻辑不同） | 12+ 天 |
| C: 桥接生成器 | +2 | 0% | 0 | 4.5-6.5 天 |

- 方案 A：每域新增 `*.Primitives.SourceGenerators` + pack + test + smoke = +4 项目 × 8 域 = +32 项目
- 方案 B：目标 `IObservable<T>` 直接生成，但仍需 +32 项目支撑
- 方案 C：模仿 Primitives 的 `R3BridgeGenerator` 模式，编译期检测 `Signal<T>` 符号后 emit 桥接扩展。最轻量，但仍需为每域手写桥接源码（`FromAsync`/`FromEvent`/`Create` 的 Primitives 实现）

### 3. 价值与风险

| 维度 | 评估 |
|------|------|
| 采用率 | 6 GitHub star，1 fork，近期版本 0 NuGet 下载 |
| API 稳定性 | 约 1 个月内 5 个 major 版本（v1→v5），频繁破坏性变更 |
| 维护团队 | Glenn Watson + Chris Pulman（ReactiveUI 核心团队，有能力） |
| 依赖方向 | Observables → Primitives（Primitives 破坏 → 8 域全挂） |
| 目标用户 | "想要反应式但不要 R3 也不要 System.Reactive"——几乎不存在的市场 |
| R3 已覆盖零分配 | R3 本身就是零分配高性能库，Primitives 无增量价值 |
| net462-481 支持 | Primitives 支持 .NET Framework，R3 不支持——唯一真实差异点 |

价值评分：1/5；风险评分：4/5。

### 4. 竞争定位

```
主流:          System.Reactive (成熟，高分配，104k 日下载)
性能导向:      R3 (快速，自创 Observable<T> 类型，增长中)
AOT/trimming:  ReactiveUI.Primitives (零依赖，保留 IObservable<T>，6 star)
```

- "零依赖反应式源生成"是一个真实但极小的 niche（NativeAOT / trimming 敏感场景）
- 24 包替代 16 包 = 更多维护、更多用户困惑
- Primitives 类型系统（`Signal<T>`、`ISequencer`、`RxVoid`）与 R3/System.Reactive 都不同，增加认知负担
- 更高 ROI 的方向：更多域（Redis/Kafka/RabbitMQ）、改进 AOT 支持、更好的 DX

## 决定

**SKIP — 当前不采用 ReactiveUI.Primitives 作为第三后端。**

四个维度一致指向"不采用"：
1. API 不兼容——Primitives 缺少生成器依赖的静态工厂方法
2. 工程代价高——朴素方案 +32 项目 / 50% CI 膨胀
3. 风险远大于价值——6 star / 0 下载 / 1 月 5 major 版本 = 不稳定基础
4. 稀释焦点——AOT niche 真实但小，24 包增加维护负担

## 重新评估条件

以下任一条件满足时重新考虑：

| 条件 | 阈值 |
|------|------|
| Primitives 采用率 | 100+ star，10,000+ NuGet 下载 |
| API 稳定性 | 12 个月无 major 版本变更 |
| 用户需求 | 有明确的 feature request 要求 Primitives 后端 |
| Primitives 补齐 API | 添加 `Observable.FromAsync`/`Create`/`FromEvent` 静态工厂 + `Unit` 类型 |
| NativeAOT 加速 | .NET NativeAOT 成为主流需求，且 R3/System.Reactive 无法满足 |

## 轻量替代（如未来有少量需求）

桥接生成器方案（方案 C）：仅 +2 项目，在现有 16 包中嵌入一个编译期检测的桥接生成器。当消费者同时引用了 `Observables.<Feature>.R3` 和 `ReactiveUI.Primitives` 时，自动生成 `AsPrimitivesSignal<T>()` / `AsR3Observable<T>()` 转换扩展。不改变现有生成器，只是让 Primitives 用户能桥接到 R3 后端的生成输出。工期约 2-3 天。

## 参考

- ReactiveUI.Primitives 本地克隆: `RefReps/Repositories/ReactiveUI/Primitives`
- 上游: https://github.com/reactiveui/Primitives
- Primitives R3Bridge 生成器（方案 C 参考模式）: `src/ReactiveUI.Primitives.R3Bridge.Generator/R3BridgeGenerator.cs`
- Observables 生成器 emission 示例: `Observables.RestAPI/.../Emitter.cs` (lines 215-234)
