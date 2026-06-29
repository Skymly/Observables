# Observables.RestAPI.R3

Declarative type-safe HTTP client with Roslyn source generators — annotate interfaces with `[Get]`/`[Post]` attributes to generate [R3](https://github.com/Cysharp/R3) `Observable<T>` proxies. Refit-style reactive REST API client.

## Install

```xml
<PackageReference Include="Observables.RestAPI.R3" Version="0.1.2" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.RestAPI;
using R3;

public interface IUserApi
{
    [Get("/users/{id}")]
    Observable<User> GetUser(int id);
}

var api = RestService.For<IUserApi>(httpClient);
User user = await api.GetUser(42).FirstAsync();
```

## Diagnostics

`OBS3001`–`OBS3005` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
