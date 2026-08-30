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

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceive<T>(ClientWebSocket socket) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                try
                {
                    var message = await WebSocketProtocol.ReceiveMessageAsync(socket, ct).ConfigureAwait(false);
                    if (message is null)
                    {
                        observer.OnCompleted();
                        return;
                    }

                    observer.OnNext(WebSocketProtocol.DeserializePayload<T>(message.Value.Payload, message.Value.MessageType));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }
            }
        });
}
