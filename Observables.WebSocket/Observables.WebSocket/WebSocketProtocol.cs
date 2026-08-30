using System.IO;
using System.Net.WebSockets;
using System.Text;
#if NETSTANDARD2_0
#else
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
#endif

namespace Observables.WebSocket;

internal readonly struct WebSocketReceivedMessage
{
    internal WebSocketReceivedMessage(byte[] payload, WebSocketMessageType messageType)
    {
        Payload = payload;
        MessageType = messageType;
    }

    internal byte[] Payload { get; }
    internal WebSocketMessageType MessageType { get; }
}
internal static class WebSocketProtocol
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

    internal static async Task ConnectAsync(
        ClientWebSocket socket,
        Uri uri,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await socket.ConnectAsync(uri, linked.Token).ConfigureAwait(false);
    }

    internal static async Task CloseAsync(
        ClientWebSocket socket,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await socket
            .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", linked.Token)
            .ConfigureAwait(false);
    }

    internal static async Task SendAsync(
        ClientWebSocket socket,
        byte[] payload,
        WebSocketMessageType messageType,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await socket
            .SendAsync(new ArraySegment<byte>(payload), messageType, true, linked.Token)
            .ConfigureAwait(false);
    }

    internal static Task SendTextAsync(
        ClientWebSocket socket,
        string text,
        CancellationToken userToken,
        CancellationToken pumpToken) =>
        SendAsync(socket, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, userToken, pumpToken);
    internal static async Task<WebSocketReceivedMessage?> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketMessageType messageType = WebSocketMessageType.Binary;
        WebSocketReceiveResult result;
        do
        {
            result = await socket
                .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            messageType = result.MessageType;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return new WebSocketReceivedMessage(ms.ToArray(), messageType);
    }

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
