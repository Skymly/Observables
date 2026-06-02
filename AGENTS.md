# Observables — AI 代理说明

## 项目状态

- **类型**：个人项目（Skymly 工作区）
- **远端**：https://github.com/Skymly/Observables（私有）；`origin/main` 已与本地 `main` 同步；文件夹名 `Observables` = 仓库名
- **阶段**：**Events**（经典 + 路由事件，R3/Reactive 生成器）、**RestAPI** 已实现；**NuGet 发布**尚未进行
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
| **`Observables.<Feature>.Package`** | 发布时 | **打包项目（每 Feature 一个）**：产出 **`Observables.<Feature>.R3`** 与 **`Observables.<Feature>.Reactive`** 两个 NuGet 包（Events、RestAPI 占位已建，其余域待补）。 |

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
| **SignalR** 等 | 骨架 | `*.R3` 骨架 | `Observables.<Feature>` 骨架 | — |

**RestAPI 运行时**：`RestApiSettings`、`RestService.For<T>()`；命名空间 `Observables.RestAPI`。

---

## 实现顺序建议

1. 其余域 **`.Package`** 占位与生成器（SignalR、WebSocket、Mqtt、Grpc）按检查清单补齐
3. **NuGet 发布**（`Pack` / `Publish` 目标；须维护者指定版本号）

## 构建与测试

```powershell
# 与 CI 一致（Nuke）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

| Nuke 目标 | 说明 |
|-----------|------|
| **Ci** | `Clean` → `Restore` → `Compile` → **UnitTest**（显式跑全部测试项目） |
| **Pack** | 打包 `*.Package`（若项目存在且可 pack） |
| **Publish** | 推送 `artifacts/package`（需 `NUGET_API_KEY`） |

CI：`.github/workflows/ci.yml` 调用 Nuke **Ci**（`windows-latest`，.NET 8 + 10）。

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
