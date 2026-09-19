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

    /// <summary>
    /// Sends <paramref name="payload"/> when the observable is subscribed. The byte array is captured when
    /// this method is called, not when a later subscriber arrives.
    /// </summary>
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

    /// <summary>
    /// Sends <paramref name="text"/> when the observable is subscribed. The string is captured when this
    /// method is called. JSON payloads other than string/byte[] require net8.0 or later.
    /// </summary>
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

    /// <summary>
    /// Receives frames from a shared per-socket pump. Subscribe may happen before Connect; the pump waits
    /// while any sink is attached. Abort and other unrecoverable socket faults complete the stream once.
    /// A non-positive <paramref name="maxMessageBytes"/> fails this subscription immediately.
    /// One observer throwing does not stop the others.
    /// </summary>
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
                        (byte[] payload, WebSocketMessageType messageType, out T value) =>
                        {
                            value = WebSocketProtocol.DeserializePayload<T>(payload, messageType);
                            return true;
                        }),
                    maxMessageBytes));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceiveNamed<T>(ClientWebSocket socket, string messageName) =>
        FromReceiveNamed<T>(socket, messageName, DefaultMaxReceiveMessageBytes);

    /// <summary>
    /// Emits only the frames carrying the <c>{"type":…}</c> envelope written by a send that declares
    /// <paramref name="messageName"/>. Frames addressed elsewhere, and frames that are not envelopes at all,
    /// are skipped rather than reported as errors.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceiveNamed<T>(ClientWebSocket socket, string messageName, int maxMessageBytes) =>
        Observable.Create<T>(observer =>
            WebSocketReceivePump
                .For(socket)
                .Subscribe(
                    new Sink<T>(
                        observer,
                        (byte[] payload, WebSocketMessageType _, out T value) =>
                            WebSocketProtocol.TryUnwrapEnvelope(payload, messageName, out value)),
                    maxMessageBytes));

    delegate bool TryDeserialize<T>(byte[] payload, WebSocketMessageType messageType, out T value);

    sealed class Sink<T>(Observer<T> observer, TryDeserialize<T> tryDeserialize)
        : IWebSocketReceiveSink
    {
        public void OnMessage(byte[] payload, WebSocketMessageType messageType)
        {
            try
            {
                if (tryDeserialize(payload, messageType, out var value))
                {
                    observer.OnNext(value);
                }
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
