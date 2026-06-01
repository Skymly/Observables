# Observables.RestAPI

声明式 REST/HTTP 客户端运行时（`Task` 与 R3 `Observable<T>`）。

## 用法

```csharp
using Observables.RestAPI;
using R3;

public interface IGitHubApi
{
    [Get("/users/{user}")]
    Task<User> GetUser(string user);

    [Get("/users/{user}")]
    Observable<User> GetUserObservable(string user);
}

var api = RestService.For<IGitHubApi>(httpClient);
```

在消费项目中引用 **`Observables.RestAPI.R3.SourceGenerators`** 或 **`Observables.RestAPI.Reactive.SourceGenerators`**（`OutputItemType="Analyzer"`），参见 `Observables.RestAPI.*.Tests` 中的 `csproj` 示例。
