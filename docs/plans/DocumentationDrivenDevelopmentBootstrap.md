# Plan: 文档驱动开发体系落地

> **状态**：Active
> **创建**：2026-07-08
> **更新**：2026-07-08
> **关联 RFC**：无（Process 变更，直接 Plan）
> **关联 Issue**：待建
> **关联 Roadmap**：P3-E11（部分）、文档体系引入

## 目标

将 [Skymly/DesignPatterns](https://github.com/Skymly/DesignPatterns) 的文档驱动开发体系适配到 Observables 主仓，建立 RFC/ADR/Spec/Design/Plan/Review 骨架与治理流程，不阻塞发版。

## 非目标

- 一次性拆分 8 域 Spec/Design（按域逐步）
- 新增 `CHANGELOG.md`（ROADMAP C3 已决定不实施）
- CI 文档链接自动校验（可选后续项）

## 里程碑拆解

| 阶段 | 内容 | 模块 | 状态 | PR |
|------|------|------|------|-----|
| P0 | `DOCUMENTATION.md`、目录骨架、模板、ADR-001 归位、`AGENTS`/`CONTRIBUTING`/PR 模板 | Docs / Repository | [x] | 本 PR |
| P1 | Events Spec + Design 拆分试点 | Events + Docs | [x] | 本 PR |
| P2 | RestAPI、SignalR Spec 拆分 | 各域 | [ ] | — |
| P3 | 其余 IO 域 Spec 拆分 | 各域 | [ ] | — |
| P4 | E5 诊断 Fix/示例补全（Observables.Docs） | Observables.Docs | [ ] | — |
| P5 | 可选：诊断 parity CI 脚本 | Solution Items | [ ] | — |

## 验收标准

- [x] `docs/DOCUMENTATION.md` 为权威标准，`AGENTS.md` 有摘要
- [x] `docs/rfc/`、`adr/`、`spec/`、`plans/`、`review/` 含模板与 README
- [x] PR 模板含 Documentation checklist
- [x] 至少 1 个域完成 Spec/Design 拆分（P1 — Events）
- [ ] Plan 状态改 `Done` 并归档

## 风险与依赖

- 迁移期存在 design（小写）与 spec（PascalCase）双轨，须在 README 标明真相源优先级
- Observables.Docs 须与 Spec 诊断表保持同步（三仓纪律）

## 变更记录

| 日期 | 调整 | 原因 |
|------|------|------|
| 2026-07-08 | 创建 Plan | 引入 DesignPatterns 文档体系 |
