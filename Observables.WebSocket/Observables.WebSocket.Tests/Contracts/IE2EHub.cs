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

    /// <summary>Send a named envelope with no payload.</summary>
    [WebSocketSend("ping")]
    Observable<Unit> Ping(CancellationToken cancellationToken = default);

    /// <summary>Send a named envelope wrapping a text payload.</summary>
    [WebSocketSend("chat")]
    Observable<Unit> SendChat(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a UTF-8 text frame.</summary>
    [WebSocketSend]
    Observable<Unit> SendText(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a binary frame.</summary>
    [WebSocketSend]
    Observable<Unit> SendBytes(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Raw text frames, unfiltered.</summary>
    [WebSocketReceive]
    Observable<string> EchoText { get; }

    /// <summary>Raw binary frames, unfiltered.</summary>
    [WebSocketReceive]
    Observable<byte[]> EchoBytes { get; }

    /// <summary>Only the envelopes <see cref="SendChat"/> writes; the echo server bounces them back.</summary>
    [WebSocketReceive("chat")]
    Observable<string> Chats { get; }
}
