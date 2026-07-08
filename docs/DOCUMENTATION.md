# 文档体系标准

> **权威源**。本文档定义本仓库**文档驱动开发（Documentation-Driven Development）**体系：所有文档的类型、结构、生命周期、归档规则，以及以文档为先导的开发流程。人类开发者和 AI 编码助手（Agent）均须遵守。`AGENTS.md`「文档体系」章节为本文档的精简摘要。
>
> - **核心原则**：**先文档后代码**——任何非琐碎变更，先确定它需要哪些文档、文档达到要求状态后才动代码（决策表见 [§11](#11-文档驱动开发流程)）。
> - **语言**：内部维护者文档以**中文**为主；面向库使用者的文档在 [Observables.Docs](https://github.com/Skymly/Observables.Docs) 仓库（英文 + `docs/zh/` 中文镜像）。
> - **三仓同步**：用户可见行为变更须同步主仓、`Observables.Docs`、`Observables.Samples`（见 `AGENTS.md` §8）。
> - **冲突优先级**：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。

---

## 1. 文档类型总览

| 类型 | 目录 | 用途 | 稳定性 | 变更门槛 |
|------|------|------|--------|----------|
| **RFC** | `docs/rfc/` | 设计提案与讨论记录 | 提案阶段，频繁迭代 | 自由修改（Review 前） |
| **ADR** | `docs/adr/` | 架构决策记录（不可变卡片） | 已决策，仅追加 | 仅 Supersede，不修改原文 |
| **Spec** | `docs/spec/` | 稳定契约（API 面、诊断 ID、不变量） | 版本化稳定 | 需 RFC + ADR 方可变更 |
| **Design Doc** | `docs/design/` | 实现细节、设计权衡、已知局限 | 随实现演进 | PR 随代码同步更新 |
| **Roadmap** | `docs/ROADMAP.md` | 功能与技术 backlog | 滚动维护 | 维护者评审 |
| **Plan** | `docs/plans/`（大型）/ GitHub Issue（小型） | 任务计划（目标、步骤、验收） | 短生命周期 | 计划内自由更新 |
| **Review** | `docs/review/` | 评审记录（设计 / 实现 / 发版回顾） | Final 后不可变 | 仅勾选行动项与修复链接 |

### 1.1 不作为独立文档类型

| 内容 | 载体 |
|------|------|
| 编码规范、兼容基线、打包、测试 | `AGENTS.md`（权威源） |
| 贡献流程、发版历史 | `CONTRIBUTING.md` |
| 变更日志 | **无独立 `CHANGELOG.md`**（见 ROADMAP C3）：GitHub Releases + `CONTRIBUTING.md` 版本表 + `ROADMAP.md` |
| 用户向使用指南 | `Observables.Docs`（VitePress） |
| 可运行示例 | `Observables.Samples` |

---

## 2. RFC — Request for Comments

### 2.1 何时需要 RFC

| 场景 | 需要 RFC？ |
|------|-----------|
| 新增功能域（新源生成器 + 诊断段） | ✅ 必须 |
| 新增或变更公共 API（破坏性） | ✅ 必须 |
| 新增诊断 ID（`OBSxxxx`） | ✅ 必须 |
| 跨域架构变更 | ✅ 必须 |
| 单域内 bug fix | ❌ Issue + PR |
| 单域内新增非破坏性 API | ❌ Issue + PR（Design Doc 记录） |
| 文档/测试/重构 | ❌ Issue + PR |
| 工程整改（CI、构建脚本） | ⚠️ 视影响面 |

### 2.2 文件命名

```
docs/rfc/<PascalCaseName>.md
```

### 2.3 Frontmatter

```markdown
> **状态**：Draft | Review | Accepted | Rejected | Implemented | Superseded
> **类型**：Feature | Domain | Architecture | Process
> **创建**：YYYY-MM-DD
> **更新**：YYYY-MM-DD
> **作者**：维护者 / 贡献者
> **关联 Roadmap**：MXX / EXX / P3-XX（如有）
> **关联 Issue**：#XXX（如有）
> **衍生 ADR**：ADR-XXX（Accepted 后填写）
```

### 2.4 生命周期

```
Draft → Review → Accepted → Implemented → (archive/)
                ↘ Rejected → (archive/)
```

模板：[`docs/rfc/_template.md`](rfc/_template.md)
索引：[`docs/rfc/README.md`](rfc/README.md)

---

## 3. ADR — Architecture Decision Record

### 3.1 规则

- 编号：`ADR-NNN-<kebab-case>.md`，三位零填充，**不复用编号**
- **Accepted 后正文不可改**；推翻须新 ADR + 旧 ADR 标 `Superseded by ADR-XXX`
- 索引与下一可用编号：[`docs/adr/README.md`](adr/README.md)

模板：[`docs/adr/_template.md`](adr/_template.md)

---

## 4. Spec — 规范文档

定义功能域的**稳定契约**：公共 API、Attribute、生成器产出形状、诊断 ID 表、不变量、兼容 TFM。

### 4.1 文件命名

```
docs/spec/<DomainName>.md
```

域名与解决方案文件夹一致：`Events`、`RestAPI`、`SignalR`、`Mqtt`、`WebSocket`、`Grpc`、`Sse`、`Nats`。

### 4.2 诊断 ID 分段（权威）

| 段 | 域 / 用途 |
|----|-----------|
| `OBS0001` | 共享分析器（R3/Reactive 包冲突） |
| `OBS2xxx` | Events 生成器 |
| `OBS3xxx` | RestAPI（`OBS3007` 为空接口分析器） |
| `OBS4xxx` | SignalR（`OBS4007` 为空接口分析器） |
| `OBS5xxx` | Mqtt（`OBS5007`） |
| `OBS6xxx` | WebSocket（`OBS6007`） |
| `OBS7xxx` | Grpc（`OBS7007`） |
| `OBS8xxx` | Sse（`OBS8007`） |
| `OBS9xxx` | Nats（`OBS9007`） |

Spec 中的诊断表须与代码 `DiagnosticDescriptors.cs`、`AnalyzerReleases.*.md` 及 [Observables.Docs diagnostics](https://github.com/Skymly/Observables.Docs/blob/main/docs/diagnostics.md) 一致。

### 4.3 双后端（R3 / Reactive）

每个 IO 域 Spec 须分别或分节描述：

- `Observables.<Domain>.R3` — `Observable<T>`（R3）
- `Observables.<Domain>.Reactive` — `IObservable<T>`（System.Reactive）

模板：[`docs/spec/_template.md`](spec/_template.md)
索引：[`docs/spec/README.md`](spec/README.md)

---

## 5. Design Doc — 设计文档

记录**实现细节**：增量生成管线、Emitter 策略、诊断检测逻辑、设计权衡、已知局限。

- 与 Spec **同名**：`docs/design/<DomainName>.md` ↔ `docs/spec/<DomainName>.md`
- **迁移中**：现有 `docs/design/<feature>.md`（小写）为历史合一文档，逐步拆分为 Spec + Design（见 [`docs/spec/README.md`](spec/README.md)）

模板：[`docs/design/_template.md`](design/_template.md)
索引：[`docs/design/README.md`](design/README.md)

横切工程文档保留在 `docs/design/` 根下（如 `public-api.md`、`shproj-dedup-plan.md`），不归入 Spec。

---

## 6. Plan 与 Review

### 6.1 Plan（`docs/plans/`）

- **用途**：跨多 PR 的大型任务（新域、ROADMAP P3 批次、文档体系迁移等）
- **里程碑表**须对齐 `AGENTS.md` 单模块 PR 边界
- 小型任务用 GitHub Issue 即可

模板：[`docs/plans/_template.md`](plans/_template.md)

### 6.2 Review（`docs/review/`）

- **用途**：RFC 设计评审、发版前审查、阶段回顾
- 单 PR code review 用 GitHub PR Comments，不必写 Review 文档

模板：[`docs/review/_template.md`](review/_template.md)

---

## 7. 归档机制（统一规则）

| 类型 | 归档目录 | 归档触发 |
|------|----------|----------|
| RFC | `docs/rfc/archive/` | Implemented / Rejected / Superseded |
| Plan | `docs/plans/archive/` | Done / Cancelled |
| Review | `docs/review/archive/` | Final 且行动项全部关闭 |
| ADR | 不移动 | Supersede 时仅改状态字段 |
| Spec / Design | 不归档 | 随实现演进；域移除时随代码删除 |

归档 = **移动文件 + 更新状态 + 更新 README 索引**，同一 PR 完成；归档后正文不改（仅修失效链接）。

---

## 8. 目录结构

```
docs/
├── DOCUMENTATION.md          # 本文件
├── README.md                 # 文档索引
├── ROADMAP.md
├── rfc/          README.md, _template.md, archive/
├── adr/          README.md, _template.md, ADR-NNN-*.md
├── spec/         README.md, _template.md, <Domain>.md
├── design/       README.md, _template.md, <Domain>.md, 横切文档
├── plans/        README.md, _template.md, archive/
└── review/       README.md, _template.md, archive/
```

---

## 9. 与用户文档站的分工

| 受众 | 位置 | 语言 |
|------|------|------|
| 库使用者 | `Observables.Docs` | 英文 + `docs/zh/` |
| 维护者 / 深度设计 | 主仓 `docs/` | 中文为主 |
| AI 约束 | 根 `AGENTS.md` | 中文 |

修改诊断 ID 或公共 API 时同步：

1. 主仓 Spec / Design / `AnalyzerReleases.Unshipped.md`
2. `Observables.Docs` 的 `docs/diagnostics.md` + `docs/zh/diagnostics.md`（及域页面）
3. 生成器 `DiagnosticHelpLink` 锚点与文档 `<span id="obsxxxx">` 一致

---

## 10. Spec / Design 迁移策略（进行中）

M1–M7 期间各域设计写在 `docs/design/<feature>.md`（合一式：API + 实现 + 诊断混排）。引入文档体系后：

| 阶段 | 动作 |
|------|------|
| **已完成** | 文档体系骨架、`DOCUMENTATION.md`、模板、ADR 归位 |
| **按域逐步** | 从 `docs/design/events.md` 等拆出 `docs/spec/Events.md` + 精简 `docs/design/Events.md` |
| **不阻塞发版** | 未拆分前，以现有 design 文档 + `Observables.Docs` 为用户向真相源 |

优先级建议：Events → RestAPI → SignalR → 其余 IO 域（与 ROADMAP P3 E5/E6 诊断文档补全对齐）。

---

## 11. 文档驱动开发流程

**先文档后代码**：动手写代码前，先按下表判定变更所需的文档前置条件。

### 11.1 变更类型 → 文档前置条件决策表

| 变更类型 | RFC | ADR | Plan | Review | 实现 PR 须同步 |
|----------|-----|-----|------|--------|----------------|
| 新增功能域（生成器 + 诊断段） | ✅ Accepted | ✅ | ✅ `docs/plans/` | ✅ 设计评审 | Spec 新建 + Design 新建 + Docs/Samples 三仓 |
| 破坏性公共 API | ✅ Accepted | ✅ | 视规模 | ✅ | Spec + `CONTRIBUTING.md` 版本说明（如发版） |
| 新增诊断 ID | ✅ Accepted | ✅ | 视规模 | 建议 | Spec（或 design 过渡期）+ `AnalyzerReleases.Unshipped.md` + Observables.Docs `diagnostics.md` |
| 非破坏性 API（单域） | ❌ | ❌ | Issue | ❌ | Design Doc + Docs（若用户可见） |
| Bug fix | ❌ | ❌ | Issue | ❌ | Docs（若用户可见）；`CONTRIBUTING` 发版时记录 |
| 重构（无行为变更） | ❌ | 视架构影响 | ❌ | ❌ | Design Doc（若结构变化） |
| 文档体系 / 工程整改 | ⚠️ Process RFC 或 Plan | 视情况 | 建议 | ❌ | 本文档 + `AGENTS.md` / `CONTRIBUTING.md` |

### 11.2 Agent 工作流约定

| 场景 | Agent 行为 |
|------|-----------|
| 新增诊断 ID | 确认是否有 Accepted RFC；无则提示创建 RFC |
| 修改公共 API | 确认 RFC + ADR；同步 Spec / Docs |
| 跨多 PR 大型任务 | 确认 `docs/plans/` 是否有 Plan |
| 创建 ADR | 编号取 `docs/adr/README.md` 下一可用编号 |
| Spec 变更 | 确认 RFC 已 Accepted；更新 Spec 版本字段（对齐 `eng/Observables.Package.props`） |
| 发版记录 | 更新 `CONTRIBUTING.md` 版本表 + `ROADMAP.md`（**不**维护 `CHANGELOG.md`） |
| 文档目录 | 不在 `docs/` 之外创建维护者设计文档（`.Local/` 除外） |

### 11.3 人类开发者工作流约定

1. 从 ROADMAP 或 Issue 认领任务，查决策表确定所需文档。
2. 需要 RFC 时先开 RFC PR（可纯文档），Review → Accepted 后再实现。
3. 实现 PR 勾选 `.github/pull_request_template.md` 的 Documentation checklist。
4. 合并前确认 Observables.Docs / Samples 是否需要配套 PR。

---

## 12. 文档质量检查清单

发版或合并大型特性前：

- [ ] 诊断 ID 在代码、`AnalyzerReleases`、Observables.Docs `diagnostics.md` 一致
- [ ] `helpLinkUri` 锚点与 Docs 页 `id="obsxxxx"` 可解析
- [ ] Spec / Design 交叉链接有效（或迁移期注明 design 文档为临时真相源）
- [ ] RFC / Plan 状态与 README 索引一致
- [ ] ADR 编号未复用
- [ ] 三仓版本号与 `eng/Observables.Package.props` 对齐（发版时）

---

## 13. 参考

- 体系原型：[Skymly/DesignPatterns `docs/DOCUMENTATION.md`](https://github.com/Skymly/DesignPatterns/blob/main/DesignPatterns/docs/DOCUMENTATION.md)
- 本仓 ROADMAP：[ROADMAP.md](ROADMAP.md)
- 诊断用户文档：[Observables.Docs diagnostics](https://skymly.github.io/Observables.Docs/diagnostics.html)
