# 设计：OBS0001 后端包冲突检测（三后端模型）

> 状态：**已实施** — 三后端拓扑下 OBS0001 的重设计已按本记录落地（声明式后端键 + 符号兜底；Primitives 槽预留，包本身未实现）。
> 关联：[ADR-005](../adr/ADR-005-primitives-backend-adopt.md)；工程债台账 `docs/research/engineering-debt-ledger-2026-10.md` §S-1。

## 1. 现状（两后端模型）

`ReactivePackageConflictDetection` 用三条启发式判断「同域双后端共存」：

| 检测腿 | 机制 | 现状问题 |
|--------|------|----------|
| `HasReactiveBridgeReference` | 编译引用含 `Observables.<F>.Reactive` 程序集 | 与包 ID 混淆：包名 = 程序集名仅在域有 `.Reactive` 桥接程序集时成立 |
| `HasObservablesR3Backend` 腿 1 | 引用 `Observables.<F>.R3` / `Observables.<F>.R3.SourceGenerators` | `Observables.RestAPI.R3` 包只 ship `Observables.RestAPI.dll`，无同名程序集；`*.SourceGenerators` 是 analyzer，不进入 `compilation.References` |
| `HasObservablesR3Backend` 腿 2 | 全局命名空间含 `Observables.<F>.Generated` | RestAPI 实际生成命名空间是 `Observables.RestAPI.Implementation`；Events 生成 `R3.SourceGenerators` / Reactive 变体 |

**结论**：RestAPI 与 Events 两个优先域三条腿全灭——同装 `.R3`+`.Reactive` 时用户只见裸 CS0101，OBS0001 从不触发。其余 8 域依赖「包恰好带同名桥接程序集」才偶发命中。

## 2. 冲突语义

三包拓扑下，冲突的本质不变：**每个后端包的生成器向消费程序集发射同名类型**（`IXxxEvents`/`XxxImpl`/bootstrap 扩展），≥2 个后端 = CS0101 重复定义。

附加约束（来自 Primitives 调研）：

- `.Primitives` 与 `.Reactive` 共存时，除同名类型外还有 `Subscribe` 扩展同名引发的 CS0121——即便类型不撞也不能共存。
- 因此模型是 **两两互斥**（同域任意 ≥2 个后端包 = OBS0001），不是「三选一」文案所能表达的更弱语义。

## 3. 提案：声明式后端信号取代符号启发式

程序集/命名空间启发式对「零 lib 包」「analyzer-only 包」本质不可靠。改走 MSBuild 声明：

1. 每个 `Observables.<F>.<Backend>` 包的 `buildTransitive` props 写入唯一键，例如
   `ObservablesRestAPIBackend_R3=true`、`..._Reactive=true`、`..._Primitives=true`。
2. 分析器读 `build_property.*`（与现有 `ObservableRoutedEvents` 同通道），同域命中 ≥2 键 → OBS0001，消息列出具体包名。
3. 开发期 `ProjectReference + OutputItemType=Analyzer`（无 props 流）保留一条符号兜底：检查生成 bootstrap 类型 `Observables.<F>.*.ObservableEventsBootstrapExtensions` 的存在性——三后端下该类型命名需带后端维度（见 §4）。

## 4. 三后端接缝要求

| 接缝 | 要求 |
|------|------|
| 生成类型命名 | 每后端的 bootstrap/接口/impl 命名必须互相区分或在同域互斥前提下允许同名（当前同名→CS0101 即「冲突」信号本身）；检测不依赖命名 |
| `ProxyDomainTable` | `ReactiveAssemblyName`/`R3PackageName` 二元模型需扩为三槽（`PrimitivesPackageName`）；`R3PackageByReactivePackageId` code-fix 映射从「切到 R3」改为「保留其一、移除其余」的多选修复 |
| `HasReactiveBridgeReference` | 改为逐后端引用检查：`Observables.<F>.R3` / `.Reactive` / `.Primitives` 桥接程序集各自独立判定 |
| 诊断消息 | OBS0001 message 从「R3 vs Reactive」改为参数化列名（`{0}` 与 `{1}` 两个包名），第三包场景报两条或一条列三个 |

## 5. 明确的非决策（留待实施 ADR/PR）

- OBS0001 的 severity/豁免机制（是否允许显式 opt-out）。
- `.Primitives` 包内 analyzer 传递的 R3Bridge.Generator 剔除方式（打包期 target，参照 Refit `RemoveReactiveUIPrimitivesR3BridgeAnalyzers`）。
- 现有 8 个未投入域是否随共享修复一并获得准确检测（默认随 `ProxyDomainTable` 扩展自动获得）。
