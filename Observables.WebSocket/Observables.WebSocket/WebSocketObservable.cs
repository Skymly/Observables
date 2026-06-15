using System.Net.WebSockets;
using System.Text;
using R3;
#if NETSTANDARD2_0
#else
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
#endif

namespace Observables.WebSocket;

/// <summary>Bridges <see cref="ClientWebSocket"/> APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class WebSocketObservable
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

    public static Observable<Unit> FromConnect(
        ClientWebSocket socket,
        Uri uri,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket.ConnectAsync(uri, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromClose(
        ClientWebSocket socket,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", linked.Token)
                .ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromSend(
        ClientWebSocket socket,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket
                .SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Binary,
                    true,
                    linked.Token)
                .ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<Unit> FromSendText(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var payload = Encoding.UTF8.GetBytes(text);
            await socket
                .SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    true,
                    linked.Token)
                .ConfigureAwait(false);
            return Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromReceive<T>(ClientWebSocket socket) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            var buffer = new byte[4096];
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                try
                {
                    // Assemble potentially fragmented message
                    using var ms = new System.IO.MemoryStream();
                    WebSocketMessageType messageType = WebSocketMessageType.Binary;
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket
                            .ReceiveAsync(new ArraySegment<byte>(buffer), ct)
                            .ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            observer.OnCompleted();
                            return;
                        }

                        messageType = result.MessageType;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var payload = ms.ToArray();
                    observer.OnNext(DeserializePayload<T>(payload, messageType));
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

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    internal static T DeserializePayload<T>(byte[] payload, WebSocketMessageType messageType)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)payload;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)Encoding.UTF8.GetString(payload);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Deserializing WebSocket payloads to types other than byte[] or string requires net8.0 or later.");
#else
        var json = Encoding.UTF8.GetString(payload);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException("WebSocket payload deserialized to null.");
        }

        return value;
#endif
    }
}
