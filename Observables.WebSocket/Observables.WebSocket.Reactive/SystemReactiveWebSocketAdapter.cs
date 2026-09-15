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
                        (payload, messageType) => WebSocketProtocol.DeserializePayload<T>(payload, messageType)),
                    maxMessageBytes));

    sealed class Sink<T>(IObserver<T> observer, Func<byte[], WebSocketMessageType, T> deserialize)
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
                observer.OnError(ex);
            }
        }

        public void OnClosed() => observer.OnCompleted();

        public void OnFailed(Exception error) => observer.OnError(error);

        public void OnTransientError(Exception error) => observer.OnError(error);
    }
}
