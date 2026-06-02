# Observables.Events.R3.SourceGenerators

.NET 事件 → [R3](https://github.com/Cysharp/R3) `Observable<T>` 的 Roslyn 源生成器（经典事件 + 可选路由事件）。

## 用法

```csharp
using Observables.Events.R3;

public class ClickSource
{
    public event Action? Click;
}

ClickSource source = new();
var stream = source.Events().Click;
```

- **Events** — 将事件转为 `R3.Observable`（按委托签名推断元素类型）
- **EventHandlers** — 使用 `EventObservable.EventHandler`（`EventHandler` / `(object, T)` 形态）
- **RoutedEvents** / **RoutedEventHandlers** / **AttachedRoutedEvent*** — 路由事件（需在项目中设置 `<ObservableRoutedEvents>true</ObservableRoutedEvents>`；WPF 另需 `UseWPF`）

生成代码位于命名空间 `Observables.Events.R3`（`internal` 接口与实现）。

## MSBuild

```xml
<PropertyGroup>
  <!-- 默认 false；启用路由/附加路由事件生成 -->
  <ObservableRoutedEvents>true</ObservableRoutedEvents>
</PropertyGroup>
```

包发布后通过 `buildTransitive` 导入 `targets/observables.events.props`（默认 `ObservableRoutedEvents=false`）。

## 诊断

| ID | 场景 |
|----|------|
| `OBS2001` | 经典 `Events` 不支持的委托 |
| `OBS2002` | 经典 `EventHandlers` 不支持的委托 |
| `OBS2003` | 路由事件不支持的委托 |
| `OBS2004` | `RoutedEventHandlers` 不支持的委托 |

## 构建与测试

```powershell
dotnet build ../Observables.slnx
dotnet test ../Observables.Events.R3.SourceGenerators.Tests
```
