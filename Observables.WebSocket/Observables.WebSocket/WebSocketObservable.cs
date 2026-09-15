using System.Net.WebSockets;
using System.Text;
using R3;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.WebSocket;

/// <summary>Bridges <see cref="ClientWebSocket"/> APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class WebSocketObservable
{
    public static Observable<Unit> FromConnect(
        ClientWebSocket socket,
        Uri uri,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.ConnectAsync(socket, uri, cancellationToken, ct).ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromClose(
        ClientWebSocket socket,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.CloseAsync(socket, cancellationToken, ct).ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromSend(
        ClientWebSocket socket,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol
                .SendAsync(socket, payload, WebSocketMessageType.Binary, cancellationToken, ct)
                .ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromSendText(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.SendTextAsync(socket, text, cancellationToken, ct).ConfigureAwait(false);
            return Unit.Default;
        });

    /// <summary>Default reassembled-receive cap (1 MiB). Exceeding it completes the receive stream with an error.</summary>
    public const int DefaultMaxReceiveMessageBytes = WebSocketProtocol.DefaultMaxReceiveMessageBytes;

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceive<T>(ClientWebSocket socket) =>
        FromReceive<T>(socket, DefaultMaxReceiveMessageBytes);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceive<T>(ClientWebSocket socket, int maxMessageBytes) =>
        Observable.Create<T>(observer =>
            WebSocketReceivePump
                .For(socket)
                .Subscribe(
                    new Sink<T>(
                        observer,
                        (payload, messageType) => WebSocketProtocol.DeserializePayload<T>(payload, messageType)),
                    maxMessageBytes));

    sealed class Sink<T>(Observer<T> observer, Func<byte[], WebSocketMessageType, T> deserialize)
        : IWebSocketReceiveSink
    {
        public void OnMessage(byte[] payload, WebSocketMessageType messageType)
        {
            try
            {
                observer.OnNext(deserialize(payload, messageType));
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        }

        public void OnClosed() => observer.OnCompleted();

        public void OnFailed(Exception error) => observer.OnCompleted(Result.Failure(error));

        public void OnTransientError(Exception error) => observer.OnErrorResume(error);
    }
}
