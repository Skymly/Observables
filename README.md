# Observables

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件。远端仓库：[github.com/Skymly/Observables](https://github.com/Skymly/Observables)（私有）；`main` 已与 `origin/main` 同步。

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
| **`Observables.<Feature>.Package`** | 发布时打包，产出上述两个 NuGet 包（尚未发布） |

## 域实现状态

| 顺序 | 域 | R3 | System.Reactive |
|------|-----|-----|-----------------|
| 1 | **Events**（经典 .NET 事件） | `Events.R3.SourceGenerators`（已实现） | `Events.Reactive.SourceGenerators`（进行中） |
| 2 | **RoutedEvents**（路由事件） | `RoutedEvents.R3`（骨架） | `Observables.RoutedEvents`（骨架） |
| 3 | **RestAPI**（自 Refit.R3 迁入） | `RestAPI` + `RestAPI.R3.SourceGenerators` | `RestAPI.Reactive` + `RestAPI.Reactive.SourceGenerators` |
| 4+ | SignalR、WebSocket、Mqtt、Grpc | 各域 `*.R3` 骨架 | 各域运行时骨架 |

## RestAPI

声明式类型安全 HTTP 客户端：`Observables.RestAPI`（运行时）+ `Observables.RestAPI.R3.SourceGenerators` 或 `Observables.RestAPI.Reactive.SourceGenerators` + 可选 `Observables.RestAPI.Reactive` / `HttpClientFactory`。

```xml
<ProjectReference Include="Observables.RestAPI" />
<ProjectReference Include="Observables.RestAPI.R3.SourceGenerators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 与 MVVMAIO / Refit.R3 的关系

- **MVVMAIO**：事件生成计划存档后迁入 Observables（**Events.R3** 已完成；对照 `MvvmAIO.R3.SourceGenerators`）。
- **Refit.R3**（`C:\Code\Refit.R3`）：**已迁入 RestAPI 域**，原仓库只读对照。

## 构建

```powershell
cd Observables
dotnet build Observables.slnx
dotnet test Observables.slnx
```

代理与贡献者请参阅 [AGENTS.md](./AGENTS.md)。
