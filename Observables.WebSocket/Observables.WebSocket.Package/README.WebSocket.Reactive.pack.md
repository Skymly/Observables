# Observables.WebSocket.Reactive

Declarative WebSocket proxies for System.Reactive `IObservable<T>` (Roslyn-generated proxies).

## Install

```xml
<PackageReference Include="Observables.WebSocket.Reactive" Version="0.1.0" />
<PackageReference Include="System.Reactive" Version="6.0.1" />
```

## Usage

```csharp
using Observables.WebSocket;
using System.Reactive;

[WebSocket]
public interface IMyWebSocketHub
{
    [WebSocketConnect]
    IObservable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

    [WebSocketClose]
    IObservable<Unit> Close(CancellationToken cancellationToken = default);

    [WebSocketSend("ping")]
    IObservable<Unit> Ping(CancellationToken cancellationToken = default);

    [WebSocketReceive("message")]
    IObservable<string> Messages { get; }
}

var socket = new ClientWebSocket();
var hub = WebSocketService.For<IMyWebSocketHub>(socket);
await hub.Connect(new Uri("wss://example.com"));
hub.Messages.Subscribe(Console.WriteLine);
```
