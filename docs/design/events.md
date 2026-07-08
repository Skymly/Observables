# Events 域 — 设计文档（实现）

> **稳定契约**见 [docs/spec/Events.md](../spec/Events.md)（API、诊断 ID、不变量）。
> 状态：**已实现**（`main`）；NuGet `Observables.Events.R3` / `Observables.Events.Reactive`（`0.1.6`）。
> 命名、打包、诊断分段以 [`AGENTS.md`](../../AGENTS.md) 为权威。

## 1. 目标与定位（摘要）

将 .NET 事件（经典 `event`、`EventHandler`、WPF/Avalonia 路由事件）桥接为反应式流。Events 是八域中**唯一的纯生成域**：`Observables.Events` 仅含 MSBuild props，桥接类型由生成器 `internal` 产出。入口方法表见 Spec。

## 2. 生成映射

### 2.1 经典事件 → `Observable<T>`

对每个被 `.Events()` 触达的类型，生成器产出：

```csharp
namespace Observables.Events.R3;

internal interface IEventsInterface_Button
{
    Observable<Unit> Click { get; }
    Observable<string> TextChanged { get; }
}

internal sealed class EventsImpl_Button : IEventsInterface_Button
{
    private readonly Button _sender;
    public EventsImpl_Button(Button sender) => _sender = sender;

    public Observable<Unit> Click =>
        EventObservable.Event(
            conversion: h => new Action(h),
            addHandler: h => _sender.Click += h,
            removeHandler: h => _sender.Click -= h);
}

internal static class ObservableEventsBootstrapExtensions
{
    public static IEventsInterface_Button Events(this Button source)
        => new EventsImpl_Button(source);
}
```

`EventObservable.Event<TDelegate, T>` 内部调用 `R3.Observable.FromEvent(...)`；Reactive 包对应桥接为 `System.Reactive.Observable.FromEventPattern(...)`。

### 2.2 EventHandler 系 → `Observable<(object?, T)>`

```csharp
public Observable<(object? sender, PropertyChangedEventArgs e)> PropertyChanged =>
    EventObservable.EventHandler<PropertyChangedEventArgs>(
        addHandler: h => _sender.PropertyChanged += h,
        removeHandler: h => _sender.PropertyChanged -= h);
```

非泛型 `EventHandler` 退化为 `Observable<(object?, EventArgs)>`。

### 2.3 路由事件（Avalonia 示例）

```csharp
internal sealed class RoutedEventsImpl_Control : IRoutedEventsInterface_Control>
{
    private readonly Control _sender;
    private readonly object? _routes;
    private readonly bool _handledEventsToo;

    public RoutedEventsImpl_Control(Control sender, object? routes = null, bool handledEventsToo = false)
    {
        _sender = sender; _routes = routes; _handledEventsToo = handledEventsToo;
    }

    public Observable<TappedEventArgs> Tapped =>
        EventObservable.Event(
            conversion: h => new EventHandler<TappedEventArgs>(h),
            addHandler: h => _sender.AddHandler<TappedEventArgs>(_sender.TappedEvent, h, _routes, _handledEventsToo),
            removeHandler: h => _sender.RemoveHandler<TappedEventArgs>(_sender.TappedEvent, h));
}
```

`routes` 对应 `RoutingStrategies`，`handledEventsToo` 控制是否接收已处理事件。

### 2.4 附加路由事件

```csharp
public static Observable<TEventArgs> AttachedRoutedEvent<TEventArgs>(
    this T source, object routedEvent, object? routes = null, bool handledEventsToo = false)
    => EventObservable.Event(
        conversion: h => new EventHandler<TEventArgs>(h),
        addHandler: h => source.AddHandler<TEventArgs>(routedEvent, h, routes, handledEventsToo),
        removeHandler: h => source.RemoveHandler<TEventArgs>(routedEvent, h));
```

### 2.5 空回退

`NullEvents`（`internal struct`）作为未发现任何事件时的回退返回值，使 `.Events().SomeEvent` 在无事件时仍可编译（取值为 `null`/默认），避免破坏性失败。Bootstrap 扩展方法在未生成对应类型时返回 `NullEvents` 实例。

### 2.6 泛型约束目标

当 `.Events()` 的接收者是泛型参数（如 `where T : INotifyPropertyChanged`）时，生成器走 `GenericConstraintTarget` 路径，按约束类型生成对应接口与实现。

## 3. 生成器管道

`ObservableEventsGenerator : IIncrementalGenerator`（R3 与 Reactive 各一份，结构一致）：

1. **PostInitializationOutput**：产出 bootstrap 源（`NullEvents`、`EventObservable`、空回退扩展方法）。
2. **SyntaxProvider**：`IsObservableEventsInstanceEntryInvocation` 过滤 `.Events()` / `.EventHandlers()` / `.RoutedEvents()` / `.RoutedEventHandlers()` / `.AttachedRoutedEvent<T>()` / `.AttachedRoutedEventHandler<T>()` 调用节点。
3. **AnalyzerConfigOptionsProvider**：读取 `build_property.ObservableRoutedEvents` 与 `build_property.UseWPF`。
4. **Combine + Collect**：候选节点 × `CompilationProvider` × 配置。
5. **RegisterSourceOutput**：
   - `CollectObservableEventTargets` 按入口类型分类收集 `ObservableEventTargetSets`。
   - `EmitInterfaceBasedSources` 为 `Events` / `EventHandlers` / `RoutedEvents` / `RoutedEventHandlers` 各产出接口 + 实现 + 扩展方法。
   - 附加路由事件按接收类型单独产出 `*.AttachedRoutedEvent.g.cs`。

`StaticObservableEventsGenerationEnabled` 当前为 `false`（编译期常量），静态事件支持延后。

## 4. 项目组成

```
Observables.Events/
├── Observables.Events/                              # 仅 MSBuild props（无运行时代码）
│   ├── Observables.Events.csproj
│   └── targets/observables.events.props             # ObservableRoutedEvents 默认 false
├── Observables.Events.Package/                      # Traversal 根，产出 2 个 NuGet 包
│   ├── Observables.Events.R3.csproj                 # PackageId = Observables.Events.R3
│   ├── Observables.Events.Reactive.Pack.csproj      # PackageId = Observables.Events.Reactive
│   └── build/
│       ├── Observables.Events.R3.props              # buildTransitive 标记
│       └── Observables.Events.Reactive.props
├── Observables.Events.R3.SourceGenerators/          # R3 生成器（IIncrementalGenerator）
│   ├── ObservableEventsGenerator.cs                 # 入口（partial）
│   ├── ObservableEventsGenerator.*.cs               # Discovery / InterfacePipeline / EventProperties
│   ├── ObservableEventsSyntaxFactory.cs             # SyntaxFactory 助手
│   ├── EventsBootstrapSyntaxFactory.cs              # Post-init bootstrap 源
│   └── ObservableEvents/
│       ├── ObservableEventsConstants.cs             # 入口方法名、命名空间常量
│       ├── ObservableEventsEntryKind.cs             # enum：Events/EventHandlers/RoutedEvents/...
│       └── ObservableEventsModels.cs                # ObservableEventTargetSets / GenericConstraintTarget
├── Observables.Events.Reactive.SourceGenerators/    # 结构同上，命名空间切 Reactive
├── Observables.Events.R3.SourceGenerators.Tests/    # VerifyXunit 快照测试
└── Observables.Events.Reactive.SourceGenerators.Tests/
```

**无双后端 shproj**：Events 域不建 `*.SourceGenerators.Shared` shproj；诊断定义在 `Observables.Events.*.SourceGenerators` 内 `DiagnosticDescriptors.cs`（OBS2xxx）。完整诊断表见 [spec/Events.md](../spec/Events.md)。

## 5. 关键设计决策

| 决策 | 理由 |
|------|------|
| **纯生成，无运行时** | Events 域无运行时状态；`Observables.Events` 仅承载 MSBuild props |
| **接口 + 实现 + 扩展方法三件套** | 类型安全的事件访问；接口便于 mock 与测试 |
| **`.Events()` vs `.EventHandlers()` 双入口** | 前者按委托签名推断载荷元组，后者归一为 `(sender, e)` 形态 |
| **路由事件默认关闭** | 避免 WPF/Avalonia 依赖污染；消费者显式开启 |
| **WPF / Avalonia 自动检测** | 通过静态字段 `{EventName}Event` 的类型元数据判定，无需显式配置 |
| **附加路由事件独立入口** | 处理「事件定义在外国类型」场景（如 `Button.ClickEvent` 在 WPF） |
| **`NullEvents` 空回退** | 未发现事件时仍可编译，避免破坏性失败 |
| **静态事件支持延后** | `StaticObservableEventsGenerationEnabled = false`，留待后续 |
| **诊断** | OBS2001–2004 为 Warning；OBS2005 为内部错误 fail-safe（见 Spec） |

## 6. 后续（v1 之外）

- 静态事件支持（`StaticObservableEventsGenerationEnabled` 翻为 `true`）。
- 路由事件的 `RoutingStrategies` 强类型枚举（当前以 `object` 传递以避免 WPF/Avalonia 编译期依赖）。
- 跨程序集事件发现（当前仅限本程序集内 `event` 成员）。
