using System;
using System.IO;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0
#else
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
#endif

namespace Observables.WebSocket.Reactive;

/// <summary>Bridges <see cref="ClientWebSocket"/> APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveWebSocketAdapter
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

    public static IObservable<System.Reactive.Unit> FromConnect(
        ClientWebSocket socket,
        Uri uri,
        CancellationToken cancellationToken = default) =>
        System.Reactive.Linq.Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket.ConnectAsync(uri, linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromClose(
        ClientWebSocket socket,
        CancellationToken cancellationToken = default) =>
        System.Reactive.Linq.Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", linked.Token)
                .ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromSend(
        ClientWebSocket socket,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        System.Reactive.Linq.Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await socket
                .SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Binary,
                    true,
                    linked.Token)
                .ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<System.Reactive.Unit> FromSendText(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken = default) =>
        System.Reactive.Linq.Observable.FromAsync(async ct =>
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
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromReceive<T>(ClientWebSocket socket) =>
        System.Reactive.Linq.Observable.Create<T>(observer =>
        {
            var cts = new CancellationTokenSource();

            _ = ReceiveLoopAsync();

            return Disposable.Create(() => cts.Cancel());

            async Task ReceiveLoopAsync()
            {
                var buffer = new byte[4096];
                try
                {
                    while (!cts.IsCancellationRequested && socket.State == WebSocketState.Open)
                    {
                        // Assemble potentially fragmented message
                        using var ms = new MemoryStream();
                        WebSocketMessageType messageType = WebSocketMessageType.Binary;
                        WebSocketReceiveResult result;
                        try
                        {
                            do
                            {
                                result = await socket
                                    .ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token)
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
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }

                        var payload = ms.ToArray();
                        try
                        {
                            observer.OnNext(DeserializePayload<T>(payload, messageType));
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    static T DeserializePayload<T>(byte[] payload, WebSocketMessageType messageType)
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
