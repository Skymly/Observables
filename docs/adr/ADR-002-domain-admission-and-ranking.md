# ADR-002: 新域准入标准与下一批域 top-N 排名

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-28 |
| **关联 Issue** | [#141](https://github.com/Skymly/Observables/issues/141)（Wayfinder 地图）；收尾 [#149](https://github.com/Skymly/Observables/issues/149) |

## 背景

Observables 在 `0.1.6` 已于 nuget.org 稳定发布：**8 域 / 16 包 / 60 个诊断**。产品进入 post-1.0 维护期前的「是否扩域、先扩谁」窗口：继续按机会主义加域会稀释双后端模型与 CI 矩阵；完全冻结又会错过生态上清晰的 IO 边界缺口。

约束与前提（charting 时锁定）：

- **域（Feature）** = 磁盘上 `Observables.<Feature>/`，产出 **两个** NuGet 包（`.R3` / `.Reactive`）。
- **形态白名单为主判据，硬否决**；不为单个候选改共享层或双后端模型（见 [ADR-001](ADR-001-primitives-backend-skip.md)）。
- **增量性仅作参考**，不作判据。
- **排序首要依据 = 生态热度**（面向 nuget.org 的公开采用），自用需求次之。
- 产物是 **一次锁完的带次序 top-N**，而非只定第一名；**不含**任何新域的实现与发版。

调研输入：[`docs/research/io-boundary-candidates.md`](../research/io-boundary-candidates.md)（35 候选）、[`docs/research/ecosystem-adoption.md`](../research/ecosystem-adoption.md)（17 个形态契合候选的采用度）。

## 决策

**采纳下列新域准入标准，并锁定下一批域 top-5 排名如下。**

### 1. 形态白名单（gating）

新域必须落在且仅落在：

1. **协议 / IO 边界代理**
2. **原生 add/remove 事件包装**
3. **稳定运行时源适配器**

不保留开放兜底。**禁止**把 ack / offset / checkpoint / lease / 状态机等运行时语义做成域 API 的一等公民。

普查标「形态：部分」的候选：若**剥离**上述禁入语义后仍落白名单三档之一 → 过；否则出局。

### 2. 准入 gating 流水线（短跑出局）

固定顺序：**形态 → 许可 → 重叠 → E2E**。任一失败立即出局。

| 步 | 判据 | 规则 |
|----|------|------|
| 形态 | 见上节 | 白名单 + 禁入语义；「部分」可剥可过 |
| 许可 | 发布包依赖链 | **禁止 copyleft**（GPL / LGPL / AGPL 等）。宽松白名单（开放）：MIT、Apache-2.0、BSD-2/3、ISC、0BSD、PostgreSQL、MS-PL 等；名单外个案审。双许可：任一侧在白名单即可。无 SPDX / 仅包内 license：人工核对，等价宽松才过，**不清则否**。测试 / 本地工具可引用 copyleft，不得进入 pack 依赖图 |
| 重叠 | 官方 / 框架等价 | **默认否决**。中口径：已有官方 codegen / 一等特性，**或**框架内一等反应式/流抽象已覆盖。翻案须同时：官方无法提供双后端统一入口，**且**另有可点名缺失能力（强类型 payload、统一诊断、跨机制同一 `For<T>` 等）。仅换返回类型不够。未翻案 ≠ 准入，不进排序池 |
| E2E | 可测性 | 硬门槛；够级 **≤ C**（A/B/C 可过，**D 出局**）。C 允许 Docker / JVM / 云 CLI；当前 CI 无 Docker **不**否决候选，流水线改动留给实现阶段 |

**不作 gating：** 增量性；零外部凭据；「必须普查标契合」（由可剥规则覆盖）。

### 3. 交付门禁（不筛候选）

域**首次**上 nuget.org 时，`.R3` 与 `.Reactive` **同版本同时**发布。开发中可暂通一端。强制的是两个 **NuGet 包 ID**；`Observables.<Feature>.Reactive` 桥接运行时项目仍**按需**。首发后的版本锁步属发版治理，本 ADR **不**裁定。

### 4. 单 OS 域

**允许**，不硬否决。分界**取严**：官方/包声明仅单 OS，**或** CI 矩阵（Windows + Ubuntu）有一边无法跑非 skip 的 E2E → 算单 OS。若实现：E2E 只在支持 OS 跑；不支持侧 skip；NuGet/文档标 `SupportedOSPlatform`（或等价）。

**排序：** 单 OS 整类排在所有**跨平台合格**候选之后；类内仍可比。

### 5. 排序口径与 top-5

过 gating 后：

1. **跨平台合格**按生态热度排序：专用包 NuGet 累计下载 + Stack Overflow 标签量；**BCL / 共享框架包降权解读**（不因传递依赖放大的累计下载压过专用包信号）。自用需求为次依据。
2. **单 OS** 整类殿后。
3. 重叠待翻案（如 GraphQL）未翻案前不算合格，不进排序池。

**锁定 N = 5（跨平台）：**

| 序 | 候选 | 要点 | 状态 |
|----|------|------|------|
| 1 | PostgreSQL LISTEN/NOTIFY | Npgsql + SO `postgresql`；契合；E2E B | **已落地**（主仓；nuget.org 目标 `0.1.7`） |
| 2 | Redis Pub/Sub | StackExchange.Redis + SO `redis`；契合；E2E A（Garnet） | **主仓已落地**（nuget.org 目标 `0.1.8`） |
| 3 | .NET 诊断源 | 诊断栈无处不在（BCL 降权后仍强）；契合；E2E A | 待实现 |
| 4 | RabbitMQ（AMQP 0-9-1） | 「部分」可剥 ack；E2E C；Apache-2.0 一侧 | 待实现 |
| 5 | AMQP 1.0 | 契合；E2E A（AMQPNetLite listener）；Apache-2.0 | 待实现 |

**§6 mid-trigger 复审（`0.1.7` / Postgres）：** #1 已发版落地触发复审。未发现可点名的采用度位次翻转、许可/官方 codegen 剧变或 gating 规则变更；**剩余次序 #2–#5 维持不变**。本复审**不**自动开 Redis epic / 实现排期。

**状态更新（主仓 Redis）：** #2 Redis Pub/Sub 已按 PRD [#169](https://github.com/Skymly/Observables/issues/169) 在主仓落地（运行时 + 双路生成器 + Garnet E2E + Shared catalog）；**不**改写 §1–§4 准入规则或 §5 排序口径。nuget.org 发版仍待维护者授权（规划 `0.1.8`）。

**单 OS 殿后（过 gating，不进 top-5）：** WMI 事件；Windows 事件日志。

### 6. 排名复审节奏

- **事件触发（中）：** gating 规则变更；锁定 top-5 中有域已发版落地；依赖许可 / 官方 codegen 局面剧变；采用度相对位次出现可点名证据的翻转；新候选通过完整 gating。
- **日历下限：** 每个 Observables **稳定发版窗口**至少复审一次。
- **产出：** 更新本 ADR 的 **§5 排名表**（或由 superseding ADR 替换）；**不自动改**实现排期。

准入判据（§1–§4）的结构性变更须 **新 ADR**（本 ADR 标 Superseded）；不得静默改写。

## 后果

### 正面

- 扩域有可机械执行的 gating，避免「形态漂亮但拖垮矩阵 / 许可 / 重叠」的候选混入排期。
- top-5 给出明确下一批次序，维护期讨论有据可依。
- 与 ADR-001 双后端模型一致：新域仍双包齐发，不引入第三后端、不为单候选改共享层。

### 负面 / 明确出局（摘录）

| 候选 | 原因 |
|------|------|
| ZeroMQ / NetMQ | 许可：LGPLv3 |
| D-Bus | 重叠：官方 codegen |
| Dapr pub/sub | 形态不契合 + 重叠 |
| Akka.NET EventStream | 重叠：框架一流抽象 |
| Orleans Streams | 重叠：框架一流抽象 |
| GraphQL Subscriptions | 重叠未翻案（可经 §2 翻案后再入池） |
| Redis Streams / Cosmos Change Feed / PG 逻辑复制 | 形态不契合 |
| SerialPort / MIDI / BLE·HID / CoAP / IMAP | E2E D |
| KurrentDB / EventStore | 许可不清（无 SPDX） |

完整出局叙述见 [#148](https://github.com/Skymly/Observables/issues/148)。

### 本图 Out of scope（仍成立）

- 任何新域的**实现与发版**
- 为容纳某候选而改共享层或双后端模型
- 第三反应式后端（ADR-001）
- 首发后双包版本锁步（发版治理）
- 第三类形态（稳定运行时源适配器）的共享层触点与 `OBSxxxx` 诊断段分配（实现/工程治理；白名单已含该类形态）

### 重新评估条件

见 §6。另：若 GraphQL 等重叠候选完成双重要求翻案、或许可/官方局面剧变，按复审节奏更新 §5。

## 参考

- Wayfinder 地图：[Wayfinder: 域准入标准与下一批域排序](https://github.com/Skymly/Observables/issues/141)
- [ADR-001](ADR-001-primitives-backend-skip.md)
- [`AGENTS.md`](../../AGENTS.md)
- [`docs/research/io-boundary-candidates.md`](../research/io-boundary-candidates.md)
- [`docs/research/ecosystem-adoption.md`](../research/ecosystem-adoption.md)
- 子决策：[#142](https://github.com/Skymly/Observables/issues/142)–[#148](https://github.com/Skymly/Observables/issues/148)、[#150](https://github.com/Skymly/Observables/issues/150)–[#153](https://github.com/Skymly/Observables/issues/153)
