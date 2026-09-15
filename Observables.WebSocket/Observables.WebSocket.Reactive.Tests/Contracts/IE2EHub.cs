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

    /// <summary>Send a named envelope with no payload.</summary>
    [WebSocketSend("ping")]
    IObservable<Unit> Ping(CancellationToken cancellationToken = default);

    /// <summary>Send a named envelope wrapping a text payload.</summary>
    [WebSocketSend("chat")]
    IObservable<Unit> SendChat(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a UTF-8 text frame.</summary>
    [WebSocketSend]
    IObservable<Unit> SendText(string message, CancellationToken cancellationToken = default);

    /// <summary>Send a binary frame.</summary>
    [WebSocketSend]
    IObservable<Unit> SendBytes(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Raw text frames, unfiltered.</summary>
    [WebSocketReceive]
    IObservable<string> EchoText { get; }

    /// <summary>Raw binary frames, unfiltered.</summary>
    [WebSocketReceive]
    IObservable<byte[]> EchoBytes { get; }

    /// <summary>Only the envelopes <see cref="SendChat"/> writes; the echo server bounces them back.</summary>
    [WebSocketReceive("chat")]
    IObservable<string> Chats { get; }
}
