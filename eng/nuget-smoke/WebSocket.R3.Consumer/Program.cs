using System.Net.WebSockets;
using Observables.WebSocket;
using R3;

namespace Observables.NuGetSmoke.WebSocket.R3;

[WebSocket]
public interface ISmokeHub
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

public static class Program
{
    public static void Main()
    {
        using ClientWebSocket socket = new();
        ISmokeHub hub = WebSocketService.For<ISmokeHub>(socket);
        if (hub is null)
        {
            throw new InvalidOperationException("WebSocket R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.WebSocket.R3 consumer smoke OK");
    }
}
