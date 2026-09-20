# ADR 索引

架构决策记录（Architecture Decision Record）。ADR 是不可变卡片，记录最终决策；讨论在 RFC 中完成。

- **格式与生命周期**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#adr--架构决策记录)
- **模板**：[_template.md](_template.md)
- **编号规则**：从 ADR-001 开始，零填充三位，**不复用编号**

## 决策列表

| ADR | 标题 | 状态 | 日期 | 关联 Issue / RFC |
|-----|------|------|------|------------------|
| [ADR-001](ADR-001-primitives-backend-skip.md) | 不采用 ReactiveUI.Primitives 作为第三后端 | Accepted | 2026-06-28 | — |
| [ADR-002](ADR-002-domain-admission-and-ranking.md) | 新域准入标准与下一批域 top-N 排名 | Accepted | 2026-07-28 | [#141](https://github.com/Skymly/Observables/issues/141) / [#149](https://github.com/Skymly/Observables/issues/149) |
| [ADR-003](ADR-003-backend-neutral-domain-runtime.md) | 域运行时保持后端中立，R3 桥接移入 `Observables.<Feature>.R3` | Accepted | 2026-09-15 | [#363](https://github.com/Skymly/Observables/issues/363) |

## 下一个可用编号

**ADR-004**
