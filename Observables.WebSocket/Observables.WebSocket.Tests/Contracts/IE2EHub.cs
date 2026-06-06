using Observables.WebSocket;
using R3;

namespace Observables.WebSocket.Tests.Contracts;

[WebSocket]
public interface IE2EHub
{
    [WebSocketConnect]
    Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

    [WebSocketClose]
    Observable<Unit> Close(CancellationToken cancellationToken = default);

    /// <summary>Send an empty binary frame (no payload).</summary>
    [WebSocketSend("ping")]
    Observable<Unit> Ping(CancellationToken cancellationToken = default);

    /// <summary>Send a UTF-8 text frame.</summary>
    [WebSocketSend]
    Observable<Unit> SendText(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a binary frame.</summary>
    [WebSocketSend]
    Observable<Unit> SendBytes(byte[] data, CancellationToken cancellationToken = default);

    [WebSocketReceive("echo")]
    Observable<string> EchoText { get; }

    [WebSocketReceive("bytes")]
    Observable<byte[]> EchoBytes { get; }
}
