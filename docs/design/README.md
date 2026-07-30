# Design Doc 索引

设计文档（Design Document）— 记录功能域的 API 面、诊断表、不变量、实现细节、设计权衡。

- **约定**：见 [DOCUMENTATION.md](../DOCUMENTATION.md)
- **模板**：[_template.md](_template.md)

## 功能域

| 域 | 文档 |
|----|------|
| Events | [events.md](events.md) |
| RestAPI | [restapi.md](restapi.md) |
| SignalR | [signalr.md](signalr.md) |
| Mqtt | [mqtt.md](mqtt.md) |
| WebSocket | [websocket.md](websocket.md) |
| Grpc | [grpc.md](grpc.md) |
| Sse | [sse.md](sse.md) |
| Nats | [nats.md](nats.md) |
| Postgres | [postgres.md](postgres.md) |

## 横切工程文档

| 文档 | 说明 |
|------|------|
| [architecture.md](architecture.md) | 架构总览（双后端、生成器管道、CI） |
| [contributor.md](contributor.md) | 人类贡献者指南 |
| [public-api.md](public-api.md) | Public API 分析器约定 |
| [shproj-dedup-plan.md](shproj-dedup-plan.md) | 共享项目去重计划 |

## 架构决策

已归位至 [`docs/adr/`](../adr/README.md)。原 `decisions/0001-primitives-backend.md` → [ADR-001](../adr/ADR-001-primitives-backend-skip.md)。
