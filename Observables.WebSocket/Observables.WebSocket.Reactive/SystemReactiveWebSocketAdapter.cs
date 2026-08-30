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
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var message = await WebSocketProtocol.ReceiveMessageAsync(socket, ct).ConfigureAwait(false);
                    if (message is null)
                    {
                        observer.OnCompleted();
                        return;
                    }

                    observer.OnNext(WebSocketProtocol.DeserializePayload<T>(message.Value.Payload, message.Value.MessageType));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
}
