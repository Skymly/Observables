# Observables.WebSocket.Reactive

Declarative WebSocket client proxies with Roslyn source generators — annotate interfaces with `[WebSocketReceive]`/`[WebSocketSend]` to generate [System.Reactive](https://github.com/dotnet/reactive) `IObservable<T>` proxies for realtime WebSocket messaging.

## Install

```xml
<PackageReference Include="Observables.WebSocket.Reactive" Version="0.1.1" />
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
