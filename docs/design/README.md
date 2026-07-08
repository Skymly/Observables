# Design Doc 索引

设计文档（Design Document）— 记录功能域的**实现细节**、设计权衡、已知局限。

- **格式**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#5-design-doc--设计文档)
- **模板**：[_template.md](_template.md)
- **与 Spec 的关系**：[Spec](../spec/) 描述 **what**，Design Doc 描述 **how** + **why**

## 功能域 Design Doc（迁移期 — 合一式）

以下文件同时包含 API 与实现说明，待拆分为 `docs/spec/<Domain>.md` + 精简后的 `docs/design/<Domain>.md`：

| 域 | 文档 | 目标 Spec |
|----|------|-----------|
| Events | [events.md](events.md) | [Events.md](../spec/Events.md) | — |
| RestAPI | [restapi.md](restapi.md) | `spec/RestAPI.md` |
| SignalR | [signalr.md](signalr.md) | `spec/SignalR.md` |
| Mqtt | [mqtt.md](mqtt.md) | `spec/Mqtt.md` |
| WebSocket | [websocket.md](websocket.md) | `spec/WebSocket.md` |
| Grpc | [grpc.md](grpc.md) | `spec/Grpc.md` |
| Sse | [sse.md](sse.md) | `spec/Sse.md` |
| Nats | [nats.md](nats.md) | `spec/Nats.md` |

## 横切工程文档

| 文档 | 说明 |
|------|------|
| [public-api.md](public-api.md) | Public API 分析器约定 |
| [shproj-dedup-plan.md](shproj-dedup-plan.md) | 共享项目去重计划 |

## 架构决策

已归位至 [`docs/adr/`](../adr/README.md)。原 `decisions/0001-primitives-backend.md` → [ADR-001](../adr/ADR-001-primitives-backend-skip.md)。
