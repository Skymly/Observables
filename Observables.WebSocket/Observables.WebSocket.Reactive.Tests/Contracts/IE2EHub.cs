using Observables.WebSocket;
using System.Reactive;

namespace Observables.WebSocket.Reactive.Tests.Contracts;

[WebSocket]
public interface IE2EHub
{
    [WebSocketConnect]
    IObservable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

    [WebSocketClose]
    IObservable<Unit> Close(CancellationToken cancellationToken = default);

    /// <summary>Send an empty binary frame (no payload).</summary>
    [WebSocketSend("ping")]
    IObservable<Unit> Ping(CancellationToken cancellationToken = default);

    /// <summary>Send a UTF-8 text frame.</summary>
    [WebSocketSend]
    IObservable<Unit> SendText(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a binary frame.</summary>
    [WebSocketSend]
    IObservable<Unit> SendBytes(byte[] data, CancellationToken cancellationToken = default);

    [WebSocketReceive("echo")]
    IObservable<string> EchoText { get; }

    [WebSocketReceive("bytes")]
    IObservable<byte[]> EchoBytes { get; }
}
