# Observables — AI 代理说明

本文件为在本仓库工作的 AI 编码助手提供上下文。修改代码前请先阅读本文档。文档约定见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

## 项目状态

- **类型**：个人项目（Skymly 工作区）
- **远端**：https://github.com/Skymly/Observables（私有）；文件夹名 `Observables` = 仓库名；同步状态以 `git status` 为准
- **阶段**：**Events**、**RestAPI**、**SignalR**、**Mqtt**、**WebSocket**、**Grpc**、**Sse**、**Nats**、**Postgres**、**Redis** 已实现（运行时 + 双路生成器 + 测试）；共享层另含 `Observables.CodeFixes` 与 `Observables.Analyzers`；**nuget.org 已发** `0.1.7`（**18 包**，含 Postgres）；Redis 主仓已落地，nuget.org 规划 **`0.1.8`**（+2 → **20** 包，须维护者授权）；Nuke `PackVerify` + `eng/nuget-smoke` 覆盖 manifest 包清单
- **下一里程碑**：post-1.0 维护期（M1–M7 全部完成，0.1.7 稳定版已发）；**Redis** 发版门槛见 [`docs/ROADMAP.md`](docs/ROADMAP.md) P1b / 规划 `0.1.8`
- **路线图**：里程碑与发版顺序见 [`docs/ROADMAP.md`](docs/ROADMAP.md)（M1 ✅ … M7 ✅，0.1.7 = Postgres 第九域已发；Redis = 第十域主仓已落地）
- **结构约定**：下文「仓库结构」与命名约定为权威；**工程治理**（包管理、警告、诊断、版本来源）见下文同名章节

## 目标

实现一组 **Roslyn 源生成器**，将多种事件与 IO 边界桥接到反应式 API。

---

## 命名约定（权威）

### 两层名称：解决方案项目 vs NuGet 包

| 层级 | 用途 | 命名 |
|------|------|------|
| **解决方案内项目** | 维护、CI、测试引用 | 长名、带 `.SourceGenerators` / `.Package` 等后缀 |
| **NuGet 包 ID** | 应用 `PackageReference` | **仅两个**：`Observables.<Feature>.R3`、`Observables.<Feature>.Reactive` |

文档中写 `Observables.Events.R3` 时须标明指 **NuGet 包** 还是 **`Observables.Events.R3.SourceGenerators` 项目**，避免歧义。

### 每个 Feature 的项目组成

| 项目 | 是否必需 | 角色 |
|------|----------|------|
| **`Observables.<Feature>`** | 按需 | **域运行时**。纯生成、无运行时的域（如 Events）可不建。 |
| **`Observables.<Feature>.Reactive`** | 按需 | **System.Reactive 桥接运行时**（如 `IObservable` 适配器）。桥接类型放在此项目。 |
| **`Observables.<Feature>.SourceGenerators.Shared`** | 双生成器时 | 本域共享生成器逻辑（`.projitems`），由 R3 与 Reactive 两路生成器 Import。 |
| **`Observables.<Feature>.R3.SourceGenerators`** | 是 | R3 源生成器（`IsRoslynComponent`）。 |
| **`Observables.<Feature>.Reactive.SourceGenerators`** | 是 | System.Reactive 源生成器。 |
| **`Observables.<Feature>.Package`** | 发布时 | **Traversal 根** + 两个可 pack 子项目（`Observables.<Feature>.R3.csproj` 等），产出 **`Observables.<Feature>.R3`** 与 **`Observables.<Feature>.Reactive`**。Events、RestAPI 已实现；其余域待补。 |

可选扩展（**不**算第三个消费者主包）：如 `Observables.RestAPI.HttpClientFactory`，依赖域运行时，不捆绑生成器。

### 全库共享项目

| 项目 | 角色 |
|------|------|
| **`Observables.Core`** | 全库通用**运行时**（≥2 个 Feature 复用的 Attribute、枚举、接口等）。不引用 Roslyn。 |
| **`Observables.SourceGenerators.Shared`** | 全库通用**生成器**基础设施（`BackendTokens`、`GeneratedSourceHeader`、符号扩展、跨域可复用诊断如 Events `OBS2xxx`）。不引用 R3 / System.Reactive。 |
| **`Observables.Analyzers`** | 独立分析器（非生成器）：全库诊断 `OBS0001`（R3/Reactive 包冲突）、各域空代理接口 `OBS4007`/`OBS5007`/`OBS6007`/`OBS7007` 等。随 `.Package` 以 analyzer 形式分发。 |
| **`Observables.CodeFixes`** | 对应分析器/生成器诊断的 `CodeFixProvider` 与补全提供器。随 `.Package` 以 analyzer 形式分发。 |

### 反应式后端规则

| NuGet 包（目标） | 运行时 | 生成器项目 |
|------------------|--------|------------|
| **`Observables.<Feature>.R3`** | R3 | `*.R3.SourceGenerators` |
| **`Observables.<Feature>.Reactive`** | System.Reactive + 本域 `.Reactive`（若有） | `*.Reactive.SourceGenerators` |

- R3 包 **不** 引用 System.Reactive；Reactive 包 **不** 引用 R3。
- 生成器仅编译期；发布后消费者通过 **`.Package` 元包** 获得「运行时 + 对应分析器」。开发阶段用 `ProjectReference` + `OutputItemType="Analyzer"`。

### 运行时类型放在哪

```
≥2 个 Feature 复用     →  Observables.Core
仅单域使用             →  Observables.<Feature>（按需创建）
IObservable 等桥接     →  Observables.<Feature>.Reactive（按需；与 Reactive 包一起发布）
```

### `.Package` 项目（每 Feature 一个）

- **一个** `Observables.<Feature>.Package` 负责该域 **两个** NuGet 包（`PackageId` = `.R3` 与 `.Reactive`）。
- 每个包应包含：对应运行时、对应分析器 DLL、`buildTransitive` props/targets（若需要）。
- 参考 Skymly 内 `MvvmAIO.Markup.Pack`；**不要**拆成两个 `.Package` 项目（除非日后明确变更）。

### 消费者引用示例

**R3（目标 NuGet）：**

```xml
<PackageReference Include="Observables.RestAPI.R3" Version="…" />
```

**System.Reactive（目标 NuGet）：**

```xml
<PackageReference Include="Observables.RestAPI.Reactive" Version="…" />
```

开发与测试阶段：

```xml
<ProjectReference Include="..\Observables.RestAPI.R3.SourceGenerators\Observables.RestAPI.R3.SourceGenerators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

### 新增 Feature 检查清单

1. 是否需要 `Observables.<Feature>` 运行时？
2. 是否需要 `Observables.<Feature>.Reactive` 桥接？
3. 建立 `*.SourceGenerators.Shared`（若两路生成器共享逻辑）
4. 建立 `*.R3.SourceGenerators` 与 `*.Reactive.SourceGenerators`（**生成器项目必须带 `.SourceGenerators` 后缀**，`.R3`/`.Reactive` 仅用于 NuGet 包 ID）
5. 建立 `*.Package`，产出 `.R3` / `.Reactive` 两个包；在 [`eng/Observables.BuildManifest.json`](eng/Observables.BuildManifest.json) 登记 `packProject` + `packageId`
6. 诊断 ID 按域分段（见「诊断治理」段分配表，如 Events `OBS2xxx`、RestAPI `OBS3xxx`、Grpc 预留 `OBS7xxx`），并在 `AnalyzerReleases.Unshipped.md` 登记
7. 补齐测试矩阵（生成器测试 + E2E + `eng/nuget-smoke` 消费者），并加入 `eng/Observables.BuildManifest.json` 的 `testProjects` / `smokeConsumers`
8. 同步文档（主仓 README、Observables.Docs、Observables.Samples）

---

## 仓库结构

```
Observables/
├── Observables.SourceGenerators.props                # 仓库根 MSBuild
├── Observables.SourceGenerators.R3.props
├── Observables.Shared/
│   ├── Observables.Core/
│   └── Observables.SourceGenerators.Shared/
├── Observables.Events/                               # 域文件夹 = Observables.<Feature>
│   ├── Observables.Events/Observables.Events.csproj  # 运行时 + targets/（同名子夹，避免 SDK  glob 同级项目）
│   ├── Observables.Events.Package/
│   ├── Observables.Events.SourceGenerators.Shared/   # shproj（双路生成器共享，#if EVENTS_R3 切换）
│   ├── Observables.Events.R3.SourceGenerators/
│   └── …
├── Observables.RestAPI/
│   ├── Observables.RestAPI/Observables.RestAPI.csproj
│   ├── Observables.RestAPI.Reactive/
│   ├── Observables.RestAPI.SourceGenerators.Shared/
│   └── …
├── Observables.SignalR/ … Observables.Grpc/          # 其余域（含 Grpc，M3 已落地）
└── Observables.slnx
```

磁盘上**每个 Feature 一个父目录** `Observables.<Feature>/`，其下为同名或后缀项目文件夹；`slnx` 中 `/Events/`、`/RestAPI/` 等虚拟文件夹与物理目录对应，勿在仓库根再并列散落 `Observables.<Feature>.*` 项目夹。

### 解决方案文件夹（`Observables.slnx`）

| 文件夹 | 内容 |
|--------|------|
| **Solution Items** | `AGENTS.md`、`README.md`、`CONTRIBUTING.md`、公共 MSBuild props |
| **Shared** | `Observables.Core`、`Observables.SourceGenerators.Shared` |
| **Events** | 双路生成器、测试、`Events.Package`；`Observables.Events/Observables.Events/targets/observables.events.props`（`ObservableRoutedEvents` 默认 `false`） |
| **RestAPI** / **SignalR** / … | 该域全部项目；RestAPI 含 `SourceGenerators.Shared`（shproj，`Id` 固定）、`RestAPI.Package`、**Tests** |
| **RestAPI/Tests** | `RestAPI.Tests`、`Reactive.Tests`、`GeneratorTests`、`HttpClientFactory.Tests` |

新增域时在 slnx 中增加同名 `/Feature/` 文件夹，勿按 R3/Reactive 横向分组。

### 域实现状态（摘要）

| 域 | 运行时 | R3 生成器 | Reactive 生成器 | 测试 |
|----|--------|-----------|-----------------|------|
| **RestAPI** | `Observables.RestAPI` | `RestAPI.R3.SourceGenerators` | `RestAPI.Reactive.SourceGenerators` | Core / Reactive / Generator / HCF |
| **Events** | `Observables.Events`（props） | `Events.R3.SourceGenerators` | `Events.Reactive.SourceGenerators` | R3/Reactive 生成器测试（经典 + 路由；路由需 `ObservableRoutedEvents=true`）；双路生成器共享 `Events.SourceGenerators.Shared` shproj（`#if EVENTS_R3` 切换后端） |
| **SignalR** | `Observables.SignalR` | `SignalR.R3.SourceGenerators` | `SignalR.Reactive.SourceGenerators` | R3 + Reactive 生成器测试 |
| **Mqtt** | `Observables.Mqtt` | `Mqtt.R3.SourceGenerators` | `Mqtt.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；`Mqtt.Tests` / `Mqtt.Reactive.Tests`（进程内 MQTTnet broker E2E） |
| **WebSocket** | `Observables.WebSocket` | `WebSocket.R3.SourceGenerators` | `WebSocket.Reactive.SourceGenerators` | Core / Reactive / R3 + Reactive 生成器测试；E2E（`WebSocket.Tests` / `WebSocket.Reactive.Tests`） |
| **Grpc** | `Observables.Grpc` | `Grpc.R3.SourceGenerators` | `Grpc.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；E2E（`Grpc.Tests` / `Grpc.Reactive.Tests`，进程内 `TestServer`） |
| **Sse** | `Observables.Sse` | `Sse.R3.SourceGenerators` | `Sse.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；E2E（`Sse.Tests` / `Sse.Reactive.Tests`，内嵌 HTTP server） |
| **Nats** | `Observables.Nats` | `Nats.R3.SourceGenerators` | `Nats.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；E2E（`Nats.Tests` / `Nats.Reactive.Tests`，进程内 nats-server） |
| **Postgres** | `Observables.Postgres` | `Postgres.R3.SourceGenerators` | `Postgres.Reactive.SourceGenerators` | R3/Reactive 生成器测试；E2E（`Postgres.Tests` / `Postgres.Reactive.Tests`，B-tier portable peer）；`Postgres.Package` + PackVerify + nuget-smoke |
| **Redis** | `Observables.Redis` | `Redis.R3.SourceGenerators` | `Redis.Reactive.SourceGenerators` | R3/Reactive 生成器测试；E2E（`Redis.Tests` / `Redis.Reactive.Tests`，进程内 Garnet）；`Redis.Package`；PackVerify / nuget-smoke / CI 见 #176 |

**RestAPI 运行时**：`RestApiSettings`、`RestService.For<T>()`；命名空间 `Observables.RestAPI`。

**Grpc 运行时**：`GrpcService.For<T>()`、`[Grpc]` / `[GrpcUnary]` 等；命名空间 `Observables.Grpc`。

---

## 实现顺序建议

里程碑级排序以 [`docs/ROADMAP.md`](docs/ROADMAP.md) 为准；本节为速记：

1. ~~**M1**：WebSocket 发版 + 文档/示例同步~~ ✅（`0.1.0-preview5`）
2. ~~**M2**：工程加固（中央包管理、TFM 收口、警告策略、诊断登记、`build/Program.cs` 去硬编码）~~ ✅
3. ~~**M3**：Grpc 域按检查清单补齐（含骨架重命名、`OBS7xxx`）~~ ✅
4. ~~**M4**：Observables.Docs / Samples 与主仓同步~~ ✅
5. **M7**：Public API 基线（`PublicAPI.Shipped.txt`）+ 维护者推 `0.1.0` tag（见 [`docs/design/public-api.md`](docs/design/public-api.md) 与「版本、Tag 与 NuGet」）

---

## 工程治理（权威）

本章为项目级开发规范。每条以「现状 → 问题 → 标准」给出：**标准**为目标态，部分尚未落地的项已列入 [`docs/ROADMAP.md`](docs/ROADMAP.md) M2。落地改动按「跨模块 PR 边界」拆分，**改动 props / 包版本 / `build/` 归入 Solution Items 模块**。

### 1. MSBuild 与包管理

- **中央包管理（CPM）**
  - 已落地：[`Directory.Packages.props`](Directory.Packages.props) 集中 22 个包版本；主树 csproj/props 已去除 `Version` 属性。`eng/nuget-smoke/` 通过本地 `Directory.Packages.props`（`ManagePackageVersionsCentrally=false`）保持动态/显式版本，模拟真实消费者。
  - 标准：新增依赖先写入 `Directory.Packages.props`；csproj 仅写 `<PackageReference Include="…" />` 不带 `Version`。同一依赖**全仓单一版本**（含 Roslyn `4.12.0`、analyzer roslyn 文件夹 `roslyn4.12`）。
- **公共属性归位**
  - 已落地：[`Directory.Build.props`](Directory.Build.props) 导入 [`eng/Observables.ProjectDefaults.props`](eng/Observables.ProjectDefaults.props)，按项目类型自动设置 TFM、`IsPackable`、AOT 标记；`Nullable`/`LangVersion`/`ImplicitUsings` 在仓库根统一声明，各 csproj 已去除重复。
  - **Package 子目录链式导入**：`Observables.<Feature>.Package/Directory.Build.props` 须 `Import` 仓库根 `Directory.Build.props`（MSBuild 只自动导入最近一层）；否则 pack 子项目拿不到 CPM 与 ProjectDefaults。
  - 标准：csproj 仅保留 `RootNamespace`、`Description`、`IsPackable`（pack 为 `true`）等差异属性；生成器 TFM 仍由 [`Observables.SourceGenerators.props`](Observables.SourceGenerators.props) 在 csproj 导入后覆盖。
- **props 分层职责**
  - [`Observables.SourceGenerators.props`](Observables.SourceGenerators.props)：生成器/分析器公共项（`netstandard2.0`、`IsRoslynComponent`、`EnforceExtendedAnalyzerRules`、Roslyn 包引用、`ObservablesReactiveBackend=SystemReactive`）。
  - [`Observables.SourceGenerators.R3.props`](Observables.SourceGenerators.R3.props)：导入上者并切 `ObservablesReactiveBackend=R3`，同时注入 `OBSERVABLES_R3`（供共享 `BackendTokens` 与 IO 域 Parser；域级 `*_R3` 仍可用于 BridgeType 等）。
  - [`eng/Observables.Package.props`](eng/Observables.Package.props)：打包元数据 + **唯一 `Version`/`PackageVersion`**。
  - 标准：新增项目按用途导入对应 props，不在 csproj 复制其内容。

### 2. 警告策略

- 已落地：`eng/Observables.ProjectDefaults.props` 对非 skip 项目启用 `TreatWarningsAsErrors=true`（`nuget-smoke`、`.Package`、`_build` 等 skip 项除外）。
- 已清零（M2/M7）：RestAPI nullable（CS86xx）；xUnit1051；Events RS1032 消息格式；域运行时 net8/9 IL trim 告警（Requires* 传播 + 生成代理 `DynamicDependency`）。
- 标准：新增 CS / 分析器告警须在 PR 内修复；禁止无注释的全仓 `NoWarn`。
- **Public API（M7）**：域运行时 + Reactive 桥接（14 项目）启用 `Microsoft.CodeAnalysis.PublicApiAnalyzers`；`PublicAPI.Shipped.txt` / `Unshipped.txt` 与 per-TFM 补充文件见 [`docs/design/public-api.md`](docs/design/public-api.md)。Events（纯生成器）、`.Package`、测试不在范围。发版前将 `Unshipped` 迁入 `Shipped`；破坏性变更须 major bump。

### 3. 诊断治理

- 已落地（release 跟踪）：各诊断宿主项目旁维护 `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`；已移除 `#pragma warning disable RS2008`。
- 现状（结构）：每域 `…SourceGenerators.Shared/DiagnosticDescriptors.cs`（OBS2xxx–9xxx 域内诊断）；`Observables.Analyzers/DiagnosticDescriptors.cs`（OBS0001 包冲突 + 各域空接口 `OBS*007`，由集中式 `EmptyProxyInterfaceAnalyzer` 使用）。
- 标准：
  - **段分配（权威）**：

    | 段 | 域 |
    |----|----|
    | `OBS0001` | Shared 全库（包冲突等） |
    | `OBS2xxx` | Events |
    | `OBS3xxx` | RestAPI |
    | `OBS4xxx` | SignalR |
    | `OBS5xxx` | Mqtt |
    | `OBS6xxx` | WebSocket |
    | `OBS7xxx` | Grpc |
    | `OBS8xxx` | Sse |
    | `OBS9xxx` | Nats |
    | `OBS10xxx` | Postgres |
    | `OBS11xxx` | Redis |

  - 新增诊断落入对应段，**不复用、不跨段**。
  - 新增诊断写入对应项目的 `AnalyzerReleases.Unshipped.md`；发版时移入 `Shipped.md`（**已启用**，勿再 `#pragma warning disable RS2008`）。
  - 用户文档 `diagnostics.md`（Observables.Docs）须与代码登记表一致。

### 4. 版本单一真相源

- 已落地：[`eng/Observables.Package.props`](eng/Observables.Package.props) 的 `<PackageVersion>` / `<Version>` 为默认版本；[`build/Program.cs`](build/Program.cs) 经 `PackageVersionReader` 读取，无字面量回退。环境变量 `VERSION` 或 Nuke `--version` 仍可覆盖（发版/紧急重发）。
- 标准：CI/发版以 tag 与该 props 的一致性为门槛（见「维护者发版」）；不得在其他文件硬编码版本回退。

### 5. 构建脚本（Nuke）约定

- 已落地：pack / 测试 / smoke 清单收敛至 [`eng/Observables.BuildManifest.json`](eng/Observables.BuildManifest.json)（`packages[].packProject` + `packageId`、`testProjects`、`smokeConsumers`）；[`build/Program.cs`](build/Program.cs) 仅加载该清单。
- 标准：新增域只改 manifest 一处并跑 `CiPack`；`packageId` 须与 `.Package` 子项目的 `PackageId` 一致。

### 6. 命名一致性

- 已落地（M3）：Grpc 生成器为 `Observables.Grpc.R3.SourceGenerators` / `Observables.Grpc.Reactive.SourceGenerators`；NuGet 包 ID 为 `Observables.Grpc.R3` / `Observables.Grpc.Reactive`（由 `.Package` 产出）。
- 标准（重申「命名约定」）：
  - `Observables.<Feature>.R3` / `.Reactive` = **NuGet 包 ID**（由 `.Package` 产出）。
  - 生成器项目**必须**带 `.SourceGenerators` 后缀。
  - **文件夹名 = 项目名 = 程序集名**，整仓一致。

### 7. 测试约定

- 三层测试，新域须覆盖：
  1. **生成器测试**：快照/字符串断言生成代码与诊断（`*.R3.SourceGenerators.Tests` / `*.Reactive.SourceGenerators.Tests`）。
  2. **运行时 / E2E**：进程内服务端往返（如 Mqtt 用 MQTTnet broker、WebSocket 用本机 server、SignalR 用 Hub）。
  3. **smoke 消费者**：`eng/nuget-smoke/<Feature>.{R3,Reactive}.Consumer` 以打包产物验证端到端引用。
- 所有测试项目须登记进 `eng/Observables.BuildManifest.json` 的 `testProjects`（slnx 不保证 `dotnet test` 全量发现）。

### 8. 文档同步纪律

- 任一域的状态/版本变化，**三处必须同步**：
  1. 主仓 [`README.md`](README.md) 功能域说明；版本与包清单见 [`CONTRIBUTING.md`](CONTRIBUTING.md)。
  2. Observables.Docs（中英；新域加对应页，更新 `diagnostics.md`、`reference.md`）。
  3. Observables.Samples（新域加 `Observables.Samples.<Feature>`）。
- 维护者设计文档在 `docs/`（见 [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md)）；面向用户的使用文档在 Observables.Docs。
- 此项是发版门槛之一（见 ROADMAP「发版门槛清单」）。

---

## 文档体系

完整约定见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。本节为 Agent 精简摘要。

| 载体 | 目录 / 位置 | 用途 |
|------|-------------|------|
| **ADR** | `docs/adr/` | 架构决策记录（不可变；编号不复用；推翻须新 ADR） |
| **Design Doc** | `docs/design/` | 每域一份：API 面、诊断表、不变量、生成器管道、设计决策 |
| **Roadmap** | `docs/ROADMAP.md` | 功能与技术 backlog |
| **Issue / PR** | GitHub | 需求追踪、变更审查 |
| **Release** | GitHub Releases + `CONTRIBUTING.md` 版本表 | 版本历史 |
| **用户文档** | [Observables.Docs](https://github.com/Skymly/Observables.Docs) | 使用指南、诊断说明（VitePress） |

**无 `CHANGELOG.md`**（ROADMAP C3）。

横切文档：[`docs/design/architecture.md`](docs/design/architecture.md)（架构）、[`docs/design/contributor.md`](docs/design/contributor.md)（贡献者）。

### Agent 文档工作流

| 场景 | Agent 行为 |
|------|-----------|
| 新增诊断 ID | 更新 Design Doc + `AnalyzerReleases.Unshipped.md` + Observables.Docs `diagnostics.md` |
| 修改公共 API | 更新 Design Doc；破坏性变更须先与用户讨论并记 ADR |
| 创建 ADR | 编号取 `docs/adr/README.md` 下一可用编号；使用 `docs/adr/_template.md` |
| 发版记录 | 更新 `CONTRIBUTING.md` 版本表 + `ROADMAP.md`（**不**维护 `CHANGELOG.md`） |
| 维护者设计文档 | 仅放在 `docs/`（`.Local/` 除外） |

---

## 版本、Tag 与 NuGet（代理与维护者）

### 代理（规划与执行）

本仓库 Tag / 版本号约定如下：

| 场景 | 代理行为 |
|------|----------|
| 用户**未**提及新版本号 / tag | 计划与实现中**不得**写入默认 tag、**不得**改 `eng/Observables.Package.props` 等处的 `PackageVersion`、**不得**执行 `git tag` / `git push --tags` / `Publish` / `gh release create` |
| 用户**明确**给出版本（如 `0.1.0-preview1`） | 可将打包工程、文档、CI 配置对齐到该版本；仍**不**擅自打 tag 或推 NuGet，除非用户当次任务明确要求 |
| 发版说明、PR 描述 | 可列出「合并后由维护者执行的命令」草稿；标注为**待批准**步骤 |

**CI 不会在 PR 或 push `main` 时 Publish**；仅验证与打包 artifact（见下节）。

### 预览版 vs 稳定版（发版产物）

| 版本类型 | Git tag（`v*`） | NuGet（nuget.org + GitHub Packages） | GitHub Release |
|----------|-----------------|--------------------------------------|----------------|
| **预览**（如 `0.1.0-preview1`） | **要** | **要** | **不要** |
| **稳定**（无 `-preview` 等预发布后缀） | **要** | **要** | **要**（维护者批准；可附 `.nupkg`） |

- **预览版**：只打 tag 并推 NuGet；**禁止** `gh release create`、禁止为预览 tag 开 GitHub Release、禁止在 `release.yml` 为预览 tag 上传 Release 附件。
- **稳定版**：tag + NuGet 后，维护者可另建 GitHub Release（非 CI 自动步骤，除非日后单独约定）。

### 维护者发版（tag 触发，对齐 MvvmAIO.Markup）

1. 在 `main` 上确认 `eng/Observables.Package.props` 中的 **`PackageVersion` 与 tag 一致**（tag 为 `v` + 版本号，如 `v0.1.0-preview1`）。
2. 配置仓库 Secrets：`NUGET_API_KEY`；`GITHUB_TOKEN`（或 PAT，`packages:write`，用于 GitHub Packages）。
3. 推送 **annotated tag**（须 `v` 前缀）：

```powershell
git tag -a v0.1.0-preview1 -m "0.1.0-preview1"
git push origin v0.1.0-preview1
```

4. [`.github/workflows/release.yml`](.github/workflows/release.yml) 在 **`push` `v*` tag** 时运行 Nuke **`Publish`**（`PackVerify` → nuget.org + GitHub Packages）。仅允许维护者账号（workflow 内 `github.actor` 校验）。**不**创建 GitHub Release。
5. 紧急重发可用 **workflow_dispatch** 并手动填写 `version`（仍受 actor 限制；同样**不**发 GitHub Release）。

## 构建与测试

```powershell
# 与 CI 一致（Nuke）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

| Nuke 目标 | 说明 |
|-----------|------|
| **Ci** | `Clean` → `Restore` → `Compile` → **UnitTest** |
| **Test** | 同 `Ci`（`Compile` + `UnitTest`）；可附加 `--test-domains <逗号分隔>` 过滤测试项目（例如 `--test-domains mqtt,shared`） |
| **Pack** | 打包 pack 子项目 → `artifacts/package/`（**不**依赖 UnitTest）；可附加 `--pack-domains <逗号分隔>` 过滤包（按 `PackageId` 前缀 `Observables.<d>.` 匹配） |
| **PackOnly** | `Pack` + `PackVerify`（**不**跑 UnitTest） |
| **PackVerify** | 断言 nupkg 含 analyzer、Events `observables.events.props`、RestAPI/SignalR/Mqtt/WebSocket/Sse/Grpc/Nats/Postgres/Redis `lib/`（`0.1.7` nuget.org **18** 包；Redis 登记后 manifest **20** 包） |
| **CiPack** | CI 完整流水线：`Test` + `PackOnly` + `NuGetConsumerSmoke`（本地包）；保留供本地或 release.yml 全链路调试 |
| **Publish** | 推送到 nuget.org（`NUGET_API_KEY`）与 GitHub Packages（`GITHUB_TOKEN`，`packages:write`）；`DependsOn(Test, PackVerify)` 确保发版前测试已通过 |

| Workflow | 触发 | 作用 |
|----------|------|------|
| [`ci.yml`](.github/workflows/ci.yml) | PR / push `main` | **changes** job（`dorny/paths-filter`）→ 域 `test-domain` 矩阵（含 postgres / redis；**Shared 改动 → 全域跑**）+ `test-shared`（仅 Shared 改动时跑）+ 域 `pack-domain` 矩阵（push main / PR 带 `pack` label，且域命中）；各 job **完全并行**，未命中的域整列跳过 |
| [`release.yml`](.github/workflows/release.yml) | push tag `v*` / `workflow_dispatch` | **Publish**（须 Secrets + 维护者 actor；内部 `Test` → `PackVerify` → push） |

## 工作约定

- 仅修改本仓库，除非用户明确要求跨仓库改动（Observables.Docs / Observables.Samples 须单独 PR，见「三仓同步」）。
- 用户沟通默认 **简体中文**；公开 API 与诊断消息可用英文。
- 新增 Feature 时遵循上文检查清单；**文件夹名 = 项目名 = 程序集名**（含 `.SourceGenerators` 等后缀）。
- 重命名项目或公共 API 前须与用户确认。

### 本仓库跨模块 PR 的模块边界

与 [`Observables.slnx`](Observables.slnx) 一致；**每个模块单独 Issue + PR**：

| 模块 | 范围 |
|------|------|
| **Shared** | `Observables.Core`、`Observables.SourceGenerators.Shared`、`Observables.Analyzers`、`Observables.CodeFixes` |
| **Events** | `/Events/`（含 `Observables.Events/targets`、Shared 诊断 `OBS2001`–`OBS2005`） |
| **RestAPI** | `/RestAPI/`（含 `SourceGenerators.Shared`、Tests） |
| **SignalR** / **WebSocket** / **Mqtt** / **Grpc** / **Sse** / **Nats** / **Postgres** / **Redis** | 各对应文件夹 |
| **Docs** | 本仓 `docs/`（维护者/中文）；用户文档站 [Observables.Docs](https://github.com/Skymly/Observables.Docs)（VitePress，**分 PR**） |
| **Repository (root README)** | 根 `README.md`、`CONTRIBUTING.md` |
| **Solution Items** | 根 `AGENTS.md`、`Observables.slnx`、`Directory.Build.props`、`Directory.Packages.props`（CPM）、公共 props、`eng/`、`build/`（Nuke）、`.github/` |

跨域且跨模块时拆多个 Issue → PR；勿在同一 PR 混合 Shared 与多域改动。

---

## Git / Issue / PR / Commit

- **语言（权威）**：Issue / PR / Commit **一律英语**；与用户对话默认**简体中文**。本条为权威表述，**覆盖** `CONTRIBUTING.md` 中任何「中英文均可」的旧措辞。
- 分支：功能 `feature/<short-description>`、修复 `fix/<short-description>`、文档 `docs/<short-description>`；提交信息祈使句、说明 **why**。
- **每个 PR 只改一个模块**（边界见上文「跨模块 PR / Issue 边界」）。
- Issue 模板：[`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/)（Bug / Feature / Generator）。
- PR 模板：[`.github/pull_request_template.md`](.github/pull_request_template.md)（含 Documentation checklist）。
- **禁止**在 Commit / PR 中提及 AI / Agent 工具。
- 未经用户明确要求不得 `commit` / `push` / 发版。

---

## 澄清与规范

Agent 行为准则——与「与用户沟通」并行生效：

1. **用户表述不清楚时，立刻询问**：不要基于猜测继续工作。用聚焦的问题（而非开放式提问）澄清意图，提供 2–4 个具体选项供用户选择。
2. **用户表述不合理时，立刻指出并给出建议**：包括但不限于——违反已有 ADR（如 [ADR-001](docs/adr/ADR-001-primitives-backend-skip.md) 引入第三反应式后端、R3 包引用 System.Reactive 或 Reactive 包引用 R3、单域产出第三个 NuGet 消费者包）、复用或改变已发布 `OBSxxxx` 诊断 ID 的语义、跳过测试（生成器 Verify 快照 / 域 E2E / `eng/nuget-smoke`）、单 PR 混合多个模块、破坏兼容基线（TFM / Roslyn 版本）、未同步三仓用户文档、新建根级 `CHANGELOG.md`（ROADMAP C3 已否决）、在生成器项目中混用 R3 与 Reactive 后端逻辑。指出问题时必须说明**为什么不合理**，并给出合理替代方案。
3. **不要盲目执行**：即使能「做到」用户要求的事，如果认为方向有误，应先提出异议，等待用户确认后再动手。
4. **发现矛盾时主动报告**：如果用户的新要求与已有 ADR / `AGENTS.md` 规则冲突，指出冲突点，由用户决定是否更新规则或调整需求（ADR 变更须走 Supersede 流程，见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)）。

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档默认英语。

## Agent skills

### Issue tracker

GitHub Issues, routed by surface: library → `Skymly/Observables`; user Docs → `Skymly/Observables.Docs`; Samples → `Skymly/Observables.Samples`; GitPulse only when that repo is the work. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five roles: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repository using root `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.
