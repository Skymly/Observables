# Observables.RestAPI.Reactive

Declarative HTTP client interfaces for `System.Reactive` `IObservable<T>`.

## Install

```xml
<PackageReference Include="Observables.RestAPI.Reactive" Version="0.1.0" />
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
