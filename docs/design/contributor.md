# 贡献者指南

> 面向**人类维护者与贡献者**。AI 代理规则见 [`AGENTS.md`](../../AGENTS.md)；完整文档工作流见 [`DOCUMENTATION.md`](../DOCUMENTATION.md)。

## 1. 开始之前

### 环境

| 工具 | 要求 |
|------|------|
| .NET SDK | 8.0+（仓库 CI 亦装 9.x / 10.x 用于矩阵） |
| Git | 2.x |
| IDE | Visual Studio 2022、Rider 或 VS Code + C# Dev Kit |

### 克隆与构建

```powershell
git clone https://github.com/Skymly/Observables.git
cd Observables
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

与 CI 一致；改打包时加 `--target PackVerify`。

### 三仓布局（可选）

若需同步文档与示例，将 sibling 仓置于同级：

```
Skymly/Observables/
├── Observables/          # 本仓
├── Observables.Docs/
└── Observables.Samples/
```

## 2. 贡献流程

1. 在 GitHub 开 **Issue**（Bug / Feature / Generator 模板）。
2. 从 `main` 拉分支：`feature/…`、`fix/…` 或 `docs/…`。
3. **一个 PR 只改一个解决方案模块**（见下表）。
4. PR 标题与描述用**英文**；勾选 [PR 模板](../../.github/pull_request_template.md) 与 Documentation checklist。
5. 确保 CI 通过（或说明为何某 job 可跳过）。

| 模块 | 典型路径 |
|------|----------|
| Shared | `Observables.Shared/`、`Observables.Analyzers`、`Observables.CodeFixes` |
| Events | `Observables.Events/` |
| RestAPI / SignalR / … | 各 `Observables.<Feature>/` |
| Docs（维护者） | 主仓 `docs/` |
| Solution Items | `eng/`、`build/`、`.github/`、根 props |

用户向文档变更在 **Observables.Docs** 单独 PR；示例在 **Observables.Samples** 单独 PR。

## 3. 文档约定

按变更类型准备文档（约定见 [DOCUMENTATION.md](../DOCUMENTATION.md)）：

| 变更 | 通常需要 |
|------|----------|
| 新功能域 / 破坏性 API | ADR + Design Doc + Observables.Docs |
| 新诊断 ID | Design Doc + `AnalyzerReleases.Unshipped.md` + Observables.Docs |
| 单域小功能 / bug fix | Issue + PR；更新 Design Doc / Docs（若用户可见） |

模板：`docs/adr/_template.md`、`docs/design/_template.md`。

**无根级 CHANGELOG** — 发版时更新 [`CONTRIBUTING.md`](../../CONTRIBUTING.md) 版本表。

## 4. 新增功能域检查清单

新增域（如未来新 IO 边界）时按顺序核对（与 `AGENTS.md` 一致）：

1. [ ] 是否需要 `Observables.<Feature>` 运行时？
2. [ ] 是否需要 `Observables.<Feature>.Reactive` 桥接？
3. [ ] 建立 `*.SourceGenerators.Shared`（shproj，`#if FEATURE_R3`）
4. [ ] 建立 `*.R3.SourceGenerators` 与 `*.Reactive.SourceGenerators`
5. [ ] 建立 `*.Package`，产出两个 NuGet 包
6. [ ] 在 `eng/Observables.BuildManifest.json` 登记 pack / test / smoke
7. [ ] 分配诊断段（如 `OBS10xxx` 须先扩展 `AGENTS.md` 段表并记 ADR）
8. [ ] 各域 `AnalyzerReleases.Unshipped.md` 登记新诊断
9. [ ] 生成器测试（Verify）+ 运行时 E2E + `eng/nuget-smoke` 消费者
10. [ ] 三仓文档：主仓 `docs/design/`、Observables.Docs 域页 + `diagnostics.md`、Samples 示例项目
11. [ ] `Observables.slnx` 增加 `/Feature/` 文件夹

参考实现：**SignalR**（Hub 代理 + shproj + 双 Emitter 路径）或 **RestAPI**（HTTP 客户端）。

## 5. 诊断 ID 约定

- 段分配见 `AGENTS.md`「诊断治理」；**不复用、不跨段**。
- 新增 ID：域内 `DiagnosticDescriptors.cs` + `description` / `helpLinkUri`（`DiagnosticHelpLink`）+ `AnalyzerReleases.Unshipped.md` + Observables.Docs `diagnostics.md`（含 `#obsxxxx` 锚点）。
- 发版：将 Unshipped 规则移入 Shipped。

## 6. 测试

| 层级 | 说明 |
|------|------|
| 生成器 | `*.SourceGenerators.Tests`，Verify 快照（改生成代码后接受 `*.verified.txt`） |
| 运行时 / E2E | 进程内 server/broker（Mqtt、Grpc、Nats 等） |
| Smoke | `eng/nuget-smoke/*.Consumer` |
| 共享分析器 | `Observables.Analyzers.Tests` |

```powershell
# 单域
dotnet run --project build/_build.csproj -- --target Test --test-domains signalr

# 单包验证
dotnet run --project build/_build.csproj -- --target PackVerify --pack-domains events
```

## 7. 发版（维护者）

见 [`CONTRIBUTING.md` § Maintainer publishing](../../CONTRIBUTING.md)。代理与 AI **不得**擅自改版本、打 tag 或推 NuGet，除非用户明确要求。

## 8. 进一步阅读

- [architecture.md](architecture.md) — 双后端、生成器管道、CI
- [DOCUMENTATION.md](../DOCUMENTATION.md) — 文档类型与归档
- [ROADMAP.md](../ROADMAP.md) — 当前 backlog（P3 E-items）
- [plans/DocumentationDrivenDevelopmentBootstrap.md](../plans/DocumentationDrivenDevelopmentBootstrap.md) — 文档体系落地计划
