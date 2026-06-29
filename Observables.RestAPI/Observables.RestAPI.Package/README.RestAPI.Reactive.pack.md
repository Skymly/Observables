# Observables.RestAPI.Reactive

Declarative type-safe HTTP client with Roslyn source generators — annotate interfaces with `[Get]`/`[Post]` attributes to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies. Refit-style reactive REST API client.

## Install

```xml
<PackageReference Include="Observables.RestAPI.Reactive" Version="0.1.2" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

```csharp
using Observables.RestAPI;
using System.Reactive.Linq;

public interface IUserApi
{
    [Get("/users/{id}")]
    IObservable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>(httpClient);
User user = await api.GetUser(42).FirstAsync().ToTask();
```

## Diagnostics

`OBS3001`–`OBS3005` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
