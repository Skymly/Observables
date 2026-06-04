# Observables.SignalR.R3

Declarative SignalR hub client interfaces for [R3](https://github.com/Cysharp/R3) `Observable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.SignalR.R3" Version="0.1.0-preview4" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.SignalR;
using R3;
using Microsoft.AspNetCore.SignalR.Client;

[Hub]
public interface IChatHub
{
    [HubInvoke]
    Observable<int> GetUserCount();

    [HubOn("ReceiveMessage")]
    Observable<ChatMessage> ReceiveMessage { get; }
}

var hub = HubService.For<IChatHub>(connection);
```

## Diagnostics

`OBS4001`–`OBS4006` — see [Observables](https://github.com/Skymly/Observables).

## License

MIT
