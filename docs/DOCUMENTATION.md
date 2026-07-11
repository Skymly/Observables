# 文档约定

本文件定义本仓库的文档类型与维护规则。

## 文档载体

| 载体 | 用途 | 位置 |
|------|------|------|
| **ADR** | 架构决策记录（不可变卡片） | `docs/adr/` |
| **Design Doc** | 实现细节、API 面、诊断表、设计权衡 | `docs/design/` |
| **Roadmap** | 功能与技术 backlog | `docs/ROADMAP.md` |
| **Issue** | 需求、Bug、任务追踪 | GitHub Issues |
| **PR** | 变更审查、讨论 | GitHub Pull Requests |
| **Release** | 版本历史 | GitHub Releases + `CONTRIBUTING.md` 版本表 |
| **用户文档** | 使用指南、诊断说明 | [Observables.Docs](https://github.com/Skymly/Observables.Docs)（VitePress） |
| **示例** | 可运行 demo | [Observables.Samples](https://github.com/Skymly/Observables.Samples) |

**无独立 `CHANGELOG.md`**（ROADMAP C3）。

## ADR — 架构决策记录

- 编号：`ADR-NNN-<kebab-case>.md`，三位零填充，不复用编号
- 正文一旦接受**不可修改**；推翻须新 ADR + 旧 ADR 标 `Superseded by ADR-XXX`
- 索引与下一可用编号：[`docs/adr/README.md`](adr/README.md)
- 模板：[`docs/adr/_template.md`](adr/_template.md)

### 何时写 ADR

- 新增/变更反应式后端策略
- 新增功能域（域划分决策）
- 跨域架构变更
- 否决某个技术方向（记录为什么不做）

## Design Doc — 设计文档

每个功能域一份 `docs/design/<feature>.md`，记录：API 面、诊断 ID 表、不变量、生成器管道、项目组成、设计决策。

- 随代码 PR 同步更新
- 模板：[`docs/design/_template.md`](design/_template.md)
- 索引：[`docs/design/README.md`](design/README.md)

横切工程文档（`architecture.md`、`contributor.md`、`public-api.md` 等）保留在 `docs/design/` 根下。

## 诊断 ID 分段（权威）

| 段 | 域 |
|----|----|
| `OBS0001` | 共享（R3/Reactive 包冲突） |
| `OBS2xxx` | Events |
| `OBS3xxx` | RestAPI |
| `OBS4xxx` | SignalR |
| `OBS5xxx` | Mqtt |
| `OBS6xxx` | WebSocket |
| `OBS7xxx` | Grpc |
| `OBS8xxx` | Sse |
| `OBS9xxx` | Nats |

新增诊断落入对应段，不复用、不跨段。

## 文档同步

修改诊断 ID 或公共 API 时同步：

1. 主仓 Design Doc + `AnalyzerReleases.Unshipped.md`
2. `Observables.Docs` 的 `docs/diagnostics.md` + `docs/zh/diagnostics.md`
3. `DiagnosticHelpLink` 锚点与 Docs 页 `id="obsxxxx"` 一致

## 目录结构

```
docs/
├── DOCUMENTATION.md    # 本文件
├── README.md           # 文档索引
├── ROADMAP.md
├── adr/                # ADR（README.md, _template.md, ADR-NNN-*.md）
└── design/             # Design Doc（README.md, _template.md, <feature>.md, 横切文档）
```
