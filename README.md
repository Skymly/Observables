# Observables

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件。远端仓库：[github.com/Skymly/Observables](https://github.com/Skymly/Observables)（公开）。

里程碑与发版规划见 [docs/ROADMAP.md](docs/ROADMAP.md)；开发规范与工程治理见 [AGENTS.md](./AGENTS.md)。

## 运行时与包名

| NuGet 包 ID | 运行时 |
|-------------|--------|
| `Observables.<Feature>.Reactive` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |

每个功能域成对发布，互不混用依赖。开发与测试阶段用解决方案内项目（如 `Observables.Events.R3.SourceGenerators`）通过 `ProjectReference` + `OutputItemType="Analyzer"` 引用。

## 全库与域结构

| 层级 | 说明 |
|------|------|
| **`Observables.Core`** | 全库通用运行时（多域复用的 Attribute、枚举、接口等） |
| **`Observables.SourceGenerators.Shared`** | 全库通用生成器基础设施（诊断、符号扩展等） |
| **`Observables.<Feature>`** | 域运行时（按需；纯生成域如 Events 可不建） |
| **`Observables.<Feature>.Reactive`** | System.Reactive 桥接运行时（按需） |
| **`Observables.<Feature>.R3.SourceGenerators`** / **`.Reactive.SourceGenerators`** | 双路源生成器 |
| **`Observables.<Feature>.Package`** | 发布时打包，产出上述两个 NuGet 包 |

### 预览版 NuGet（`0.1.0-preview5`）

| 包 ID | 说明 |
|-------|------|
| `Observables.Events.R3` | Events 生成器 + R3 依赖（DevelopmentDependency） |
| `Observables.Events.Reactive` | Events 生成器 + System.Reactive 依赖 |
| `Observables.RestAPI.R3` | RestAPI 运行时 + R3 生成器 |
| `Observables.RestAPI.Reactive` | RestAPI + Reactive 桥接 + 生成器 |
| `Observables.SignalR.R3` | SignalR 运行时 + R3 生成器 |
| `Observables.SignalR.Reactive` | SignalR + Reactive 桥接 + 生成器 |
| `Observables.Mqtt.R3` | MQTT 运行时 + R3 生成器 |
| `Observables.Mqtt.Reactive` | MQTT + Reactive 桥接 + 生成器 |
| `Observables.WebSocket.R3` | WebSocket 运行时 + R3 生成器 |
| `Observables.WebSocket.Reactive` | WebSocket + Reactive 桥接 + 生成器 |

```xml
<PackageReference Include="Observables.Events.R3" Version="0.1.0-preview5" />
<PackageReference Include="R3" Version="1.3.0" />
```

从 [GitHub Packages](https://github.com/orgs/Skymly/packages) 安装时，在 `nuget.config` 中增加：

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="github" value="https://nuget.pkg.github.com/Skymly/index.json" />
</packageSources>
<packageSourceCredentials>
  <github>
    <add key="Username" value="YOUR_GITHUB_USERNAME" />
    <add key="ClearTextPassword" value="YOUR_GITHUB_PAT_WITH_PACKAGES_READ" />
  </github>
</packageSourceCredentials>
```

本地打包与校验：

```powershell
dotnet run --project build/_build.csproj -- --target PackVerify --configuration Release
```

**发布到 NuGet**（与 [MvvmAIO.Markup](https://github.com/MvvmAIO/MvvmAIO.Markup) 相同：由维护者推送 `v*` tag 触发 CI，而非 PR/main 自动发布）：

```powershell
# 1. 确认 eng/Observables.Package.props 中 PackageVersion 与 tag 一致
git tag -a v0.1.0-preview1 -m "0.1.0-preview1"
git push origin v0.1.0-preview1
# 2. GitHub Actions「Publish NuGet」workflow 使用 secrets 执行 Publish
```

本地手动推送（可选）：

```powershell
$env:VERSION = '0.1.0-preview1'
$env:NUGET_API_KEY = '...'
$env:GITHUB_TOKEN = '...'
dotnet run --project build/_build.csproj -- --target Publish --configuration Release
```

## 域实现状态

| 域 | R3 生成器 | System.Reactive 生成器 | NuGet（`preview5`） |
|----|-----------|------------------------|---------------------|
| **Events**（经典 + 路由 .NET 事件） | `Events.R3.SourceGenerators` | `Events.Reactive.SourceGenerators` | 已发 |
| **RestAPI**（声明式 HTTP 客户端） | `RestAPI.R3.SourceGenerators` | `RestAPI.Reactive.SourceGenerators` | 已发 |
| **SignalR**（Hub 代理） | `SignalR.R3.SourceGenerators` | `SignalR.Reactive.SourceGenerators` | 已发 |
| **Mqtt**（主题代理） | `Mqtt.R3.SourceGenerators` | `Mqtt.Reactive.SourceGenerators` | 已发 |
| **WebSocket**（客户端代理） | `WebSocket.R3.SourceGenerators` | `WebSocket.Reactive.SourceGenerators` | 已发 |
| **Grpc** | 空 csproj 骨架 | — | 规划中（见 ROADMAP M3） |

五域均含运行时（按需）+ 双路生成器 + 测试；共享层另有 `Observables.Analyzers` 与 `Observables.CodeFixes`。详细顺序见 [docs/ROADMAP.md](docs/ROADMAP.md)。

路由事件生成默认关闭；在消费者项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`（见 `Observables.Events/Observables.Events/targets/observables.events.props`）。

## RestAPI

声明式类型安全 HTTP 客户端：`Observables.RestAPI`（运行时）+ `Observables.RestAPI.R3.SourceGenerators` 或 `Observables.RestAPI.Reactive.SourceGenerators` + 可选 `Observables.RestAPI.Reactive` / `HttpClientFactory`。

```xml
<ProjectReference Include="Observables.RestAPI" />
<ProjectReference Include="Observables.RestAPI.R3.SourceGenerators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 构建

```powershell
cd Observables
dotnet build Observables.slnx

# 完整 CI（Nuke，与 GitHub Actions 一致）
dotnet run --project build/_build.csproj -- --target Ci --configuration Release
```

需 **.NET 10 SDK**（`global.json` 用于 Nuke `build/`）；库与测试目标为 **netstandard2.0** / **net8.0** 等，另需 **.NET 8 SDK**。

代理与贡献者请参阅 [AGENTS.md](./AGENTS.md)。
