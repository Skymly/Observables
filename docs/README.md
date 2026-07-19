# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。面向库使用者的英文/中文指南见 [Observables.Docs](https://github.com/Skymly/Observables.Docs)（VitePress：[skymly.github.io/Observables.Docs](https://skymly.github.io/Observables.Docs/)）。

## 入门

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | 文档约定（ADR、Design Doc、同步规则） |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程、发版历史、16 包清单 |
| [design/architecture.md](design/architecture.md) | 架构总览（双后端、生成器管道、CI） |
| [design/contributor.md](design/contributor.md) | 人类贡献者指南（新增域、测试、文档流程） |
| [ROADMAP.md](ROADMAP.md) | 里程碑 M1–M7、Post-1.0 backlog |
| [../AGENTS.md](../AGENTS.md) | AI 编码助手上下文（命名、诊断段、三仓同步） |

## 架构决策

| 目录 | 说明 |
|------|------|
| [adr/](adr/README.md) | ADR — 架构决策记录（[索引](adr/README.md)） |

## 功能域设计文档

| 域 | Design Doc | 用户文档 |
|----|------------|----------|
| Events | [design/events.md](design/events.md) | [Observables.Docs/events](https://skymly.github.io/Observables.Docs/events.html) |
| RestAPI | [design/restapi.md](design/restapi.md) | [restapi](https://skymly.github.io/Observables.Docs/restapi.html) |
| SignalR | [design/signalr.md](design/signalr.md) | [signalr](https://skymly.github.io/Observables.Docs/signalr.html) |
| Mqtt | [design/mqtt.md](design/mqtt.md) | [mqtt](https://skymly.github.io/Observables.Docs/mqtt.html) |
| WebSocket | [design/websocket.md](design/websocket.md) | [websocket](https://skymly.github.io/Observables.Docs/websocket.html) |
| Grpc | [design/grpc.md](design/grpc.md) | [grpc](https://skymly.github.io/Observables.Docs/grpc.html) |
| Sse | [design/sse.md](design/sse.md) | [sse](https://skymly.github.io/Observables.Docs/sse.html) |
| Nats | [design/nats.md](design/nats.md) | [nats](https://skymly.github.io/Observables.Docs/nats.html) |

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
| 真实应用 Showcase | [GitPulse](https://github.com/Skymly/GitPulse) |
| 可运行示例 | `Observables.Samples` |
| AI 约束 | 根 `AGENTS.md` |
