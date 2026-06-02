# Observables.Events.R3.SourceGenerators

经典 .NET 事件 → [R3](https://github.com/Cysharp/R3) `Observable<T>` 的 Roslyn 源生成器。

## 用法

```csharp
using Observables.Events.R3.SourceGenerators;

public class ClickSource
{
    public event Action? Click;
}

ClickSource source = new();
var stream = source.Events().Click;
```

- **Events** — 将事件转为 `R3.Observable`（按委托签名推断元素类型）
- **EventHandlers** — 使用生成命名空间内的 `EventObservable.EventHandler`（转发至 R3；`EventHandler` / `(object, T)` 形态）

生成代码位于命名空间 `Observables.Events.R3.SourceGenerators`（`internal` 接口与实现）。

## 构建与测试

```powershell
dotnet build ../Observables.slnx
dotnet test ../Observables.Events.R3.SourceGenerators.Tests
```

## 迁移说明

自 `MvvmAIO.R3.SourceGenerators` 迁入（仅经典事件；路由事件见 `Observables.RoutedEvents.R3`）。诊断码：`OBS2001`、`OBS2002`。
