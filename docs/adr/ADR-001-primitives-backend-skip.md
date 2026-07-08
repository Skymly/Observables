# ADR-001: 不采用 ReactiveUI.Primitives 作为第三后端

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-06-28 |
| **关联 RFC** | 无 — 直接决策（4 路并行调研） |

## 背景

Observables 当前支持两种反应式后端：

| 后端 | 包 ID 模式 | 运行时依赖 |
|------|-----------|-----------|
| **R3** | `Observables.<Feature>.R3` | `R3`（Cysharp，高性能，自创 `Observable<T>` 类型） |
| **System.Reactive** | `Observables.<Feature>.Reactive` | `System.Reactive`（成熟，`IObservable<T>` 生态） |

ReactiveUI.Primitives（github.com/reactiveui/Primitives）是一个零运行时依赖的反应式原语库，v5.7.0，MIT 许可。其定位为"不依赖 System.Reactive 或 R3 的高性能反应式原语"。

本 ADR 记录是否应将 Primitives 作为第三种后端集成到 Observables 的 8 域源生成器中。

## 决策

**SKIP — 当前不采用 ReactiveUI.Primitives 作为第三后端。**

四个维度一致指向"不采用"：

1. API 不兼容——Primitives 缺少生成器依赖的 `Observable.FromAsync` / `Create` / `FromEvent` 等静态工厂
2. 工程代价高——朴素方案 +32 项目 / ~50% CI 膨胀
3. 风险远大于价值——低采用率、API 不稳定
4. 稀释焦点——24 包增加维护负担，R3 已覆盖高性能场景

## 后果

- **正面**：保持 16 包双后端模型清晰；CI 与 shproj 无需三向 `#if`
- **负面**：NativeAOT 敏感且拒绝 R3/System.Reactive 的极端 niche 无一等支持
- **重新评估条件**：Primitives 采用率与 API 稳定性达标、或出现明确用户需求、或 Primitives 补齐 Rx 工厂 API

轻量替代（如未来有少量需求）：桥接生成器方案——在现有包中编译期检测 Primitives 符号并 emit 转换扩展，约 2–3 天，不改变现有 16 包布局。

## 参考

- 原评估记录：`docs/design/decisions/0001-primitives-backend.md`（已归位至本 ADR）
- ReactiveUI.Primitives：https://github.com/reactiveui/Primitives
- Observables ROADMAP 0.1.2 发版说明
