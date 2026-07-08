# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。面向库使用者的英文/中文指南见 [Observables.Docs](https://github.com/Skymly/Observables.Docs)（VitePress：[skymly.github.io/Observables.Docs](https://skymly.github.io/Observables.Docs/)）。

> **文档体系标准**：[DOCUMENTATION.md](DOCUMENTATION.md) — 定义所有文档的类型、结构、生命周期与归档规则。人类开发者和 AI 编码助手均须遵守。

## 入门

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | **文档体系标准**（类型、生命周期、模板、工作流） |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程、发版历史、16 包清单 |
| [design/architecture.md](design/architecture.md) | 架构总览（双后端、生成器管道、CI） |
| [design/contributor.md](design/contributor.md) | 人类贡献者指南（新增域、测试、文档流程） |
| [ROADMAP.md](ROADMAP.md) | 里程碑 M1–M7、Post-1.0 backlog（P2/P3） |
| [../AGENTS.md](../AGENTS.md) | AI 编码助手上下文（命名、诊断段、三仓同步） |

## 设计提案与决策

| 目录 | 说明 |
|------|------|
| [rfc/](rfc/README.md) | RFC — 设计提案（[状态板](rfc/README.md)） |
| [adr/](adr/README.md) | ADR — 架构决策记录（[索引](adr/README.md)） |

## 计划与评审

| 目录 | 说明 |
|------|------|
| [plans/](plans/README.md) | Plan — 大型任务计划（[状态板](plans/README.md)） |
| [review/](review/README.md) | Review — 评审记录（[索引](review/README.md)） |

## 规范与设计文档

| 目录 | 说明 |
|------|------|
| [spec/](spec/README.md) | Spec — 功能域稳定契约（API、诊断 ID、不变量） |
| [design/](design/README.md) | Design Doc — 实现细节、权衡、局限 |

> 8 域历史文档位于 `docs/design/<feature>.md`（小写），正逐步拆分为 `spec/` + `design/`（PascalCase），见 [spec/README.md](spec/README.md)。

## 功能域索引（迁移期）

| 域 | 当前 Design Doc | Spec | 用户文档 |
|----|-----------------|------|----------|
| Events | [design/events.md](design/events.md) | [spec/Events.md](spec/Events.md) | [Observables.Docs/events](https://skymly.github.io/Observables.Docs/events.html) |
| RestAPI | [design/restapi.md](design/restapi.md) | 待建 | [restapi](https://skymly.github.io/Observables.Docs/restapi.html) |
| SignalR | [design/signalr.md](design/signalr.md) | 待建 | [signalr](https://skymly.github.io/Observables.Docs/signalr.html) |
| Mqtt | [design/mqtt.md](design/mqtt.md) | 待建 | [mqtt](https://skymly.github.io/Observables.Docs/mqtt.html) |
| WebSocket | [design/websocket.md](design/websocket.md) | 待建 | [websocket](https://skymly.github.io/Observables.Docs/websocket.html) |
| Grpc | [design/grpc.md](design/grpc.md) | 待建 | [grpc](https://skymly.github.io/Observables.Docs/grpc.html) |
| Sse | [design/sse.md](design/sse.md) | 待建 | [sse](https://skymly.github.io/Observables.Docs/sse.html) |
| Nats | [design/nats.md](design/nats.md) | 待建 | [nats](https://skymly.github.io/Observables.Docs/nats.html) |

## 横切工程文档

| 文档 | 说明 |
|------|------|
| [design/public-api.md](design/public-api.md) | Public API 分析器与可见性约定 |
| [design/shproj-dedup-plan.md](design/shproj-dedup-plan.md) | 共享项目去重计划 |

## 与用户文档站的分工

| 受众 | 位置 |
|------|------|
| 库使用者 | `Observables.Docs` |
| 维护者 / 深度 API | 本目录 `docs/` |
| 可运行示例 | `Observables.Samples` |
| AI 约束 | 根 `AGENTS.md` |

修改诊断 ID 或公共 API 时，同步主仓 Spec/Design、`Observables.Docs` 的 [diagnostics](https://github.com/Skymly/Observables.Docs/blob/main/docs/diagnostics.md) 与各域页面。
