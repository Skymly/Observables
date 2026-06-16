# Observables.WebSocket.R3

Declarative WebSocket proxies for R3 `Observable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.WebSocket.R3" Version="0.1.0" />
<PackageReference Include="R3" Version="1.3.0" />
```

## Usage

```csharp
using Observables.WebSocket;
using R3;

[WebSocket]
public interface IMyWebSocketHub
{
    [WebSocketConnect]
    Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

    [WebSocketClose]
    Observable<Unit> Close(CancellationToken cancellationToken = default);

    [WebSocketSend("ping")]
    Observable<Unit> Ping(CancellationToken cancellationToken = default);

    [WebSocketReceive("message")]
    Observable<string> Messages { get; }
}

var socket = new ClientWebSocket();
var hub = WebSocketService.For<IMyWebSocketHub>(socket);
await hub.Connect(new Uri("wss://example.com")).FirstAsync();
await hub.Messages.FirstAsync();
```
