# Observables — AI 代理说明

## 项目状态

- **类型**：个人项目（Skymly 工作区）
- **远端**：https://github.com/Skymly/Observables（私有）；`origin/main` 已与本地 `main` 同步；文件夹名 `Observables` = 仓库名
- **阶段**：**Events**、**RestAPI**、**SignalR** 已实现；**NuGet 预览包** `0.1.0-preview3`（6 包，含包 README）；Nuke `PackVerify` + `eng/nuget-smoke` 消费者校验已就绪
- **下一 Feature**：**Mqtt**（**设计期**）；**SignalR** 已发布（NuGet `0.1.0-preview3`）；设计见 [`docs/design/mqtt.md`](docs/design/mqtt.md)；Issue [#50](https://github.com/Skymly/Observables/issues/50)
- **结构约定**：下文「仓库结构」与命名约定为权威

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
| **`Observables.SourceGenerators.Shared`** | 全库通用**生成器**基础设施（`GeneratedSourceHeader`、符号扩展、跨域可复用诊断等）。不引用 R3 / System.Reactive。 |

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
4. 建立 `*.R3.SourceGenerators` 与 `*.Reactive.SourceGenerators`
5. 建立 `*.Package`，产出 `.R3` / `.Reactive` 两个包
6. 诊断 ID 按域分段（如 Events `OBS2001`–`OBS2004`、RestAPI `OBS3001`–`OBS3005`）

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
├── Observables.SignalR/ … Observables.Grpc/          # 其余域（多为骨架）
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
| **Mqtt** | 骨架（`Observables.Mqtt`） | `Observables.Mqtt.R3` 占位 | — | — |
| **WebSocket** / **Grpc** 等 | 骨架 | `*.R3` 骨架 | — | — |

**RestAPI 运行时**：`RestApiSettings`、`RestService.For<T>()`；命名空间 `Observables.RestAPI`。

---

## 实现顺序建议

1. **Mqtt**：按 [`docs/design/mqtt.md`](docs/design/mqtt.md) 设计 Issue 链实施（Shared → R3 → Reactive → Package → Docs/Samples）
2. 其余域（**WebSocket**、**Grpc**）按检查清单补齐
3. **NuGet 发布**（见下文「版本、Tag 与 NuGet」；须维护者指定版本号并推送 tag / `workflow_dispatch`）

## 版本、Tag 与 NuGet（代理与维护者）

### 代理（规划与执行）

遵循工作区根 [`../AGENTS.md`](../AGENTS.md) 的 Tag / 版本号约定，本仓库补充：

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
| **Pack** | 打包 4 个 pack 子项目 → `artifacts/package/`（依赖 **UnitTest**） |
| **PackVerify** | 断言 nupkg 含 analyzer、Events `observables.events.props`、RestAPI/SignalR `lib/`（6 包） |
| **CiPack** | CI 用：`Pack` + `PackVerify` |
| **Publish** | 推送到 nuget.org（`NUGET_API_KEY`）与 GitHub Packages（`GITHUB_TOKEN`，`packages:write`） |

| Workflow | 触发 | 作用 |
|----------|------|------|
| [`ci.yml`](.github/workflows/ci.yml) | PR / push `main` | **Ci** + **CiPack**（测与打包 artifact，**不** Publish） |
| [`release.yml`](.github/workflows/release.yml) | push tag `v*` / `workflow_dispatch` | **Publish**（须 Secrets + 维护者 actor） |

## 工作约定

- 仅修改 `Skymly/Observables/`，除非用户明确要求跨仓库改动。
- 用户沟通默认 **简体中文**；公开 API 与诊断消息可用英文。
- 新增 Feature 时遵循上文检查清单；**文件夹名 = 项目名 = 程序集名**（含 `.SourceGenerators` 等后缀）。
- 重命名项目或公共 API 前须与用户确认。
- **Git / Issue / PR / Commit**：遵循工作区根 [`../AGENTS.md`](../AGENTS.md)。

### 本仓库跨模块 PR 的模块边界

与 [`Observables.slnx`](Observables.slnx) 一致；**每个模块单独 Issue + PR**：

| 模块 | 范围 |
|------|------|
| **Shared** | `Observables.Core`、`Observables.SourceGenerators.Shared` |
| **Events** | `/Events/`（含 `Observables.Events/targets`、Shared 诊断 `OBS2001`–`OBS2004`） |
| **RestAPI** | `/RestAPI/`（含 `SourceGenerators.Shared`、Tests） |
| **SignalR** / **WebSocket** / **Mqtt** / **Grpc** | 各对应文件夹 |
| **Solution Items** | 根 `AGENTS.md`、props、`Observables.slnx`；按变更归入 **Shared** 或相关 Feature 的 Issue |
