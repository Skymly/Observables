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
    public static void Main() => Console.WriteLine("Observables.WebSocket.R3 consumer smoke OK");
}
