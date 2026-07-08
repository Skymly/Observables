# Spec 索引

规范文档（Specification）— 定义功能域的**稳定契约**：API 面、诊断 ID、不变量、兼容基线。

- **格式与变更门槛**：见 [DOCUMENTATION.md](../DOCUMENTATION.md#4-spec--规范文档)
- **模板**：[_template.md](_template.md)
- **与 Design Doc 的关系**：Spec 描述 **what**（契约），[Design Doc](../design/) 描述 **how** + **why**（实现）

## 迁移状态

M1–M7 期间各域使用合一式 `docs/design/<feature>.md`（小写）。引入文档体系后，按域拆分为 `docs/spec/<Domain>.md` + `docs/design/<Domain>.md`（PascalCase）。

**未拆分前**：以现有 design 文档 + [Observables.Docs](https://github.com/Skymly/Observables.Docs) 为用户向真相源；新增诊断仍须同步 `AnalyzerReleases` 与 `diagnostics.md`。

## 已有 Spec

| 域 | Spec | Design Doc（当前） | 关联 ADR |
|----|------|-------------------|----------|
| Events | [Events.md](Events.md) | [events.md](../design/events.md) | — |
| RestAPI | 待建 | [restapi.md](../design/restapi.md) | — |
| SignalR | 待建 | [signalr.md](../design/signalr.md) | — |
| Mqtt | 待建 | [mqtt.md](../design/mqtt.md) | — |
| WebSocket | 待建 | [websocket.md](../design/websocket.md) | — |
| Grpc | 待建 | [grpc.md](../design/grpc.md) | — |
| Sse | 待建 | [sse.md](../design/sse.md) | — |
| Nats | 待建 | [nats.md](../design/nats.md) | — |

## 拆分优先级（建议）

与 ROADMAP P3 对齐：Events → RestAPI → SignalR → 其余 IO 域（可与 E5 诊断文档补全并行）。
