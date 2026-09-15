using System.Net.WebSockets;
using System.Reactive.Linq;
using Observables.WebSocket;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.WebSocket.Reactive;

/// <summary>Bridges <see cref="ClientWebSocket"/> APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveWebSocketAdapter
{
    public static IObservable<System.Reactive.Unit> FromConnect(
        ClientWebSocket socket,
        Uri uri,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.ConnectAsync(socket, uri, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromClose(
        ClientWebSocket socket,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.CloseAsync(socket, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromSend(
        ClientWebSocket socket,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol
                .SendAsync(socket, payload, WebSocketMessageType.Binary, cancellationToken, ct)
                .ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromSendText(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await WebSocketProtocol.SendTextAsync(socket, text, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromReceive<T>(ClientWebSocket socket) =>
        FromReceive<T>(socket, WebSocketProtocol.DefaultMaxReceiveMessageBytes);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromReceive<T>(ClientWebSocket socket, int maxMessageBytes) =>
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
    public static IObservable<T> FromReceiveNamed<T>(ClientWebSocket socket, string messageName) =>
        FromReceiveNamed<T>(socket, messageName, WebSocketProtocol.DefaultMaxReceiveMessageBytes);

    /// <summary>
    /// Emits only the frames carrying the <c>{"type":…}</c> envelope written by a send that declares
    /// <paramref name="messageName"/>. Frames addressed elsewhere, and frames that are not envelopes at all,
    /// are skipped rather than reported as errors.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromReceiveNamed<T>(ClientWebSocket socket, string messageName, int maxMessageBytes) =>
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

    sealed class Sink<T>(IObserver<T> observer, TryDeserialize<T> tryDeserialize)
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
                observer.OnError(ex);
            }
        }

        public void OnClosed() => observer.OnCompleted();

        public void OnFailed(Exception error) => observer.OnError(error);

        public void OnTransientError(Exception error) => observer.OnError(error);
    }
}
