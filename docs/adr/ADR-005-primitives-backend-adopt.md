# ADR-005: 接入 ReactiveUI.Primitives 作为第三反应式后端

| 字段 | 值 |
|------|-----|
| **状态** | Accepted（Supersedes [ADR-001](ADR-001-primitives-backend-skip.md)） |
| **日期** | 2026-10-24 |
| **关联** | [ADR-001](ADR-001-primitives-backend-skip.md)；[OBS0001 三后端设计记录](../design/obs0001-backend-conflict.md)；调研 `docs/research/reactiveui-refit-fusillade-primitives.md` |

## 背景

ADR-001 以「Primitives 缺少 `FromAsync`/`Create`/`FromEvent` 等静态工厂、工程代价高、采用率低」为由拒绝第三后端。该理由已不成立：

- Primitives v9+ 已提供 `Signal.FromAsync` 等工厂 API，生成器依赖的创建面补齐；
- 维护者重新拍板：Primitives 与 R3 / System.Reactive **地位平等**，作为第三后端接入，目标是覆盖拒绝 R3/System.Reactive 依赖、要求最小运行时足迹的消费场景。

## 决策

**采用 ReactiveUI.Primitives 作为每域第三个反应式后端。** 每域包拓扑从两包扩展为三包：

| 后端 | 包 ID 模式 |
|------|-----------|
| R3 | `Observables.<Feature>.R3` |
| System.Reactive | `Observables.<Feature>.Reactive` |
| ReactiveUI.Primitives | `Observables.<Feature>.Primitives` |

共享层与生成器接缝按「将来三包」设计（`.SourceGenerators.Shared` 第三路 `#if` 维度、`ProxyDomainTable` 三后端槽位、PackVerify 清单 20→30 包可扩展）。

## 前置障碍（实施时必须处理，不阻塞决策）

1. **analyzer 传递**：`ReactiveUI.Primitives` 传递 `R3Bridge.Generator` analyzer，其公开 marker attribute 曾致跨项目 CS0436（refit#2176/#2194）；`.Primitives` 包需等价于 Refit `RemoveReactiveUIPrimitivesR3BridgeAnalyzers` 的打包期剔除。
2. **扩展同名冲突**：Primitives 与 System.Reactive 的 `Subscribe` 扩展同名，同作用域 CS0121——`.Primitives` 与 `.Reactive` 包不可共存（与既有互斥语义一致，OBS0001 覆盖，见设计记录）。
3. **取消语义差异**：`Signal<T>` 的取消语义基于 subscription，与 `R3.Observable<T>`/`IObservable<T>` 不等价——域 API 形状设计时需逐域评估。
4. **OBS0001**：现有两后端检测需扩为两两互斥（设计记录：`docs/design/obs0001-backend-conflict.md`）。

## 后果

- **正面**：三个后端地位平等；Primitives 消费场景获得一等支持；共享接缝按三包设计后扩展成本固定。
- **负面**：包数 20→30；CI/测试矩阵膨胀 ~50%；每域 shproj 增加第三路 `#if`；`Signal<T>` 语义差异带来逐域设计成本。
- **本决策不含实施排期**——域实现顺序由 ROADMAP 另行决定。

## 参考

- [ADR-001](ADR-001-primitives-backend-skip.md)（被本 ADR 取代）
- `docs/research/reactiveui-refit-fusillade-primitives.md`（上游源码事实清单）
- `docs/research/reactiveui-overlap-decision.md`（原「不接」分析，以本决策为准）
- `docs/design/obs0001-backend-conflict.md`（OBS0001 三后端设计记录）
