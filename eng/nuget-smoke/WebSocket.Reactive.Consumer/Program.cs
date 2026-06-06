using Observables.WebSocket;
using System.Reactive;

namespace Observables.NuGetSmoke.WebSocket.Reactive;

[WebSocket]
public interface ISmokeHub
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

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.WebSocket.Reactive consumer smoke OK");
}
