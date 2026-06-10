# Observables — AI 代理说明

## 项目状态

- **类型**：个人项目（Skymly 工作区）
- **远端**：https://github.com/Skymly/Observables（私有）；文件夹名 `Observables` = 仓库名；同步状态以 `git status` 为准
- **阶段**：**Events**、**RestAPI**、**SignalR**、**Mqtt**、**WebSocket**、**Grpc** 已实现（运行时 + 双路生成器 + 测试）；共享层另含 `Observables.CodeFixes` 与 `Observables.Analyzers`；**待发版** `0.1.0-preview6`（**12 包**，含 Grpc）；主仓 / Docs / Samples 已对齐 preview6；Nuke `PackVerify` + `eng/nuget-smoke` 覆盖 12 包
- **下一里程碑**：推 tag 发布 preview6 → **M5** API 冻结 + 1.0；Grpc 设计见 [`docs/design/grpc.md`](docs/design/grpc.md)
- **路线图**：里程碑与发版顺序见 [`docs/ROADMAP.md`](docs/ROADMAP.md)（M1 ✅ / M2 ✅ / M3 ✅ / M4 部分 ✅ → M5 API 冻结 + 1.0）
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
| **`Observables.SourceGenerators.Shared`** | 全库通用**生成器**基础设施（`GeneratedSourceHeader`、符号扩展、跨域可复用诊断如 Events `OBS2xxx`）。不引用 R3 / System.Reactive。 |
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
| **Solution Items** | `AGENTS.md`、`README.md`、公共 MSBuild props |
| **Shared** | `Observables.Core`、`Observables.SourceGenerators.Shared` |
| **Events** | 双路生成器、测试、`Events.Package`；`Observables.Events/Observables.Events/targets/observables.events.props`（`ObservableRoutedEvents` 默认 `false`） |
| **RestAPI** / **SignalR** / … | 该域全部项目；RestAPI 含 `SourceGenerators.Shared`（shproj，`Id` 固定）、`RestAPI.Package`、**Tests** |
| **RestAPI/Tests** | `RestAPI.Tests`、`Reactive.Tests`、`GeneratorTests`、`HttpClientFactory.Tests` |

新增域时在 slnx 中增加同名 `/Feature/` 文件夹，勿按 R3/Reactive 横向分组。

### 域实现状态（摘要）

| 域 | 运行时 | R3 生成器 | Reactive 生成器 | 测试 |
|----|--------|-----------|-----------------|------|
| **RestAPI** | `Observables.RestAPI` | `RestAPI.R3.SourceGenerators` | `RestAPI.Reactive.SourceGenerators` | Core / Reactive / Generator / HCF |
| **Events** | `Observables.Events`（props） | `Events.R3.SourceGenerators` | `Events.Reactive.SourceGenerators` | R3/Reactive 生成器测试（经典 + 路由；路由需 `ObservableRoutedEvents=true`） |
| **SignalR** | `Observables.SignalR` | `SignalR.R3.SourceGenerators` | `SignalR.Reactive.SourceGenerators` | R3 + Reactive 生成器测试 |
| **Mqtt** | `Observables.Mqtt` | `Mqtt.R3.SourceGenerators` | `Mqtt.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；`Mqtt.Tests` / `Mqtt.Reactive.Tests`（进程内 MQTTnet broker E2E） |
| **WebSocket** | `Observables.WebSocket` | `WebSocket.R3.SourceGenerators` | `WebSocket.Reactive.SourceGenerators` | Core / Reactive / R3 + Reactive 生成器测试；E2E（`WebSocket.Tests` / `WebSocket.Reactive.Tests`） |
| **Grpc** | `Observables.Grpc` | `Grpc.R3.SourceGenerators` | `Grpc.Reactive.SourceGenerators` | R3 + Reactive 生成器测试；E2E（`Grpc.Tests` / `Grpc.Reactive.Tests`，进程内 Kestrel h2c） |

**RestAPI 运行时**：`RestApiSettings`、`RestService.For<T>()`；命名空间 `Observables.RestAPI`。

**Grpc 运行时**：`GrpcService.For<T>()`、`[Grpc]` / `[GrpcUnary]` 等；命名空间 `Observables.Grpc`。

---

## 实现顺序建议

里程碑级排序以 [`docs/ROADMAP.md`](docs/ROADMAP.md) 为准；本节为速记：

1. ~~**M1**：WebSocket 发版 + 文档/示例同步~~ ✅（`0.1.0-preview5`）
2. ~~**M2**：工程加固（中央包管理、TFM 收口、警告策略、诊断登记、`build/Program.cs` 去硬编码）~~ ✅
3. ~~**M3**：Grpc 域按检查清单补齐（含骨架重命名、`OBS7xxx`）~~ ✅
4. **M4**：Observables.Docs / Samples 与主仓同步（Grpc 用户文档与示例）
5. **M5**：API 冻结后由维护者指定版本号并推送 tag / `workflow_dispatch` 发布（见「版本、Tag 与 NuGet」）

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
  - [`Observables.SourceGenerators.R3.props`](Observables.SourceGenerators.R3.props)：导入上者并切 `ObservablesReactiveBackend=R3`。
  - [`eng/Observables.Package.props`](eng/Observables.Package.props)：打包元数据 + **唯一 `Version`/`PackageVersion`**。
  - 标准：新增项目按用途导入对应 props，不在 csproj 复制其内容。

### 2. 警告策略

- 已落地：`eng/Observables.ProjectDefaults.props` 对非 skip 项目启用 `TreatWarningsAsErrors=true`（`nuget-smoke`、`.Package`、`_build` 等 skip 项除外）。
- 已清零（M2）：RestAPI nullable（CS86xx）；xUnit1051（`TestContext.Current.CancellationToken`）；Events RS1032 消息格式。
- net8/net9 域运行时：`ProjectDefaults` 对 IL trim 族（`IL2026`/`IL3050` 等）设最小 `NoWarn`（反射 REST + JSON 序列化；M5 改 `JsonSerializerContext` + `Requires*` 传播后移除）。
- 标准：新增 CS / 分析器告警须在 PR 内修复；禁止无注释的全仓 `NoWarn`。

### 3. 诊断治理

- 已落地（release 跟踪）：各诊断宿主项目旁维护 `AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md`；已移除 `#pragma warning disable RS2008`。
- 现状（结构）：描述符仍分散——每域 `…SourceGenerators.Shared/DiagnosticDescriptors.cs`（OBS3xxx–6xxx）、Events 在 `Observables.SourceGenerators.Shared/Diagnostics/`（OBS2xxx）、`Observables.Analyzers/DiagnosticDescriptors.cs`（OBS0001 + 各域空接口 `OBS*007`）。
- 问题：单域诊断分散多文件、易撞 ID。
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
  1. 主仓 [`README.md`](README.md)「域实现状态」与预览包清单。
  2. Observables.Docs（中英；新域加对应页，更新 `diagnostics.md`、`reference.md`）。
  3. Observables.Samples（新域加 `Observables.Samples.<Feature>`）。
- 设计稿放主仓 `docs/design/<feature>.md`；面向用户的使用文档放 Observables.Docs。
- 此项是发版门槛之一（见 ROADMAP「发版门槛清单」）。

---

## 版本、Tag 与 NuGet（代理与维护者）

### 代理（规划与执行）

遵循工作区根 [`AGENTS.md`](../../../AGENTS.md) 的 Tag / 版本号约定，本仓库补充：

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
| **Pack** | 打包全部 pack 子项目 → `artifacts/package/`（依赖 **UnitTest**；当前 10 个，含 WebSocket） |
| **PackVerify** | 断言 nupkg 含 analyzer、Events `observables.events.props`、RestAPI/SignalR/Mqtt/WebSocket/Grpc `lib/`（manifest 当前 **12 包**） |
| **CiPack** | CI 用：`Pack` + `PackVerify` |
| **Publish** | 推送到 nuget.org（`NUGET_API_KEY`）与 GitHub Packages（`GITHUB_TOKEN`，`packages:write`） |

| Workflow | 触发 | 作用 |
|----------|------|------|
| [`ci.yml`](.github/workflows/ci.yml) | PR / push `main` | **Ci** + **CiPack**（测与打包 artifact，**不** Publish） |
| [`release.yml`](.github/workflows/release.yml) | push tag `v*` / `workflow_dispatch` | **Publish**（须 Secrets + 维护者 actor） |

## 工作约定

- 仅修改本仓库（工作区路径 `Skymly/Observables/Observables/`），除非用户明确要求跨仓库改动。
- 用户沟通默认 **简体中文**；公开 API 与诊断消息可用英文。
- 新增 Feature 时遵循上文检查清单；**文件夹名 = 项目名 = 程序集名**（含 `.SourceGenerators` 等后缀）。
- 重命名项目或公共 API 前须与用户确认。
- **Git / Issue / PR / Commit**：遵循工作区根 [`AGENTS.md`](../../../AGENTS.md)。

### 本仓库跨模块 PR 的模块边界

与 [`Observables.slnx`](Observables.slnx) 一致；**每个模块单独 Issue + PR**：

| 模块 | 范围 |
|------|------|
| **Shared** | `Observables.Core`、`Observables.SourceGenerators.Shared` |
| **Events** | `/Events/`（含 `Observables.Events/targets`、Shared 诊断 `OBS2001`–`OBS2004`） |
| **RestAPI** | `/RestAPI/`（含 `SourceGenerators.Shared`、Tests） |
| **SignalR** / **WebSocket** / **Mqtt** / **Grpc** | 各对应文件夹 |
| **Solution Items** | 根 `AGENTS.md`、`README.md`、`docs/`、`Observables.slnx`、`Directory.Build.props`、`Directory.Packages.props`（CPM）、公共 props、`eng/`、`build/`（Nuke）、`.github/`；按变更归入 **Shared** 或相关 Feature 的 Issue |
