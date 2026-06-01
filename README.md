# Observables

面向 **反应式编程（Rx）** 的 Roslyn 源生成器套件。

## 运行时与包名

| 包名 | 运行时 |
|------|--------|
| `Observables.<Feature>` | [System.Reactive](https://github.com/dotnet/reactive)（`IObservable<T>` 等） |
| `Observables.<Feature>.R3` | [R3](https://github.com/Cysharp/R3) |

每个功能域成对发布，互不混用依赖。

## 包规划

| 顺序 | System.Reactive | R3 |
|------|-----------------|-----|
| — | **Observables.Core**（全库通用运行时） | （同上） |
| — | **Observables.\<Feature\>.Core**（单域运行时，按需） | （同上） |
| — | **Observables.SourceGenerators.Shared**（生成器共享 Roslyn 基础设施） | （同上） |
| 1 | **Observables.Events.Reactive.SourceGenerators** | **Observables.Events.R3.SourceGenerators** |
| 2 | **Observables.RoutedEvents** | **Observables.RoutedEvents.R3** |
| 3 | **Observables.RestAPI** | **Observables.RestAPI.R3.SourceGenerators** |（**已实现**：见 `Observables.RestAPI` + 扩展包）
| 4 | **Observables.SignalR** | **Observables.SignalR.R3** |
| 5 | **Observables.WebSocket** | **Observables.WebSocket.R3** |
| 6 | **Observables.Mqtt** | **Observables.Mqtt.R3** |
| 7 | **Observables.Grpc** | **Observables.Grpc.R3** |

> 个人项目；远端 GitHub 仓库尚未创建。**Events.R3** 与 **RestAPI** 域已落地；其余域仍为生成器骨架。

## RestAPI（自 Refit.R3）

声明式类型安全 HTTP 客户端：`Observables.RestAPI`（运行时）+ `Observables.RestAPI.R3.SourceGenerators` 或 `Observables.RestAPI.Reactive.SourceGenerators` + 可选 `Observables.RestAPI.Reactive` / `HttpClientFactory`。

```xml
<ProjectReference Include="Observables.RestAPI" />
<ProjectReference Include="Observables.RestAPI.R3.SourceGenerators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## 与 MVVMAIO / Refit.R3 的关系

- **MVVMAIO**：事件生成计划存档后迁入 Observables（**Events.R3 已完成**）。
- **Refit.R3**（`C:\Code\Refit.R3`）：**已迁入 RestAPI 域**，原仓库只读对照。

## 构建

```powershell
cd Observables
dotnet build
```

代理与贡献者请参阅 [AGENTS.md](./AGENTS.md)。
