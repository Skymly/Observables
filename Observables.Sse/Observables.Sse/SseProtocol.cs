using System.IO;
using System.Net.Http;
using System.Text;
#if NETSTANDARD2_0
#else
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
#endif

namespace Observables.Sse;

/// <summary>A single parsed Server-Sent Event.</summary>
public readonly struct SseEvent
{
    public SseEvent(string eventName, string data, string? id)
    {
        EventName = eventName;
        Data = data;
        Id = id;
    }

    /// <summary>The event type ("message" when the wire <c>event</c> field is absent or whitespace).</summary>
    public string EventName { get; }

    /// <summary>The concatenated <c>data</c> payload (without the trailing newline). Empty when a <c>data:</c> field was present with no value.</summary>
    public string Data { get; }

    /// <summary>
    /// The <c>id</c> field from this dispatched block, if any.
    /// This is not the EventSource Last-Event-ID string; Observables does not persist last-id across blocks or reconnect.
    /// </summary>
    public string? Id { get; }
}

/// <summary>
/// Minimal <c>text/event-stream</c> parser shared by the R3 and System.Reactive SSE bridges.
/// Follows the WHATWG SSE field grammar (event / data / id / comment).
/// An event is dispatched only after a blank line; a half-event at EOF is dropped.
/// A <c>data:</c> field with an empty value is dispatched as empty data.
/// </summary>
public static class SseProtocol
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

    /// <summary>Reads the next dispatched event from the stream, or null at end of stream.</summary>
    public static System.Threading.Tasks.Task<SseEvent?> ReadEventAsync(StreamReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return ReadEventAsync(
            reader,
            SseConnection.DefaultMaxLineBytes,
            SseConnection.DefaultMaxEventBytes,
            CancellationToken.None);
    }

    internal static async System.Threading.Tasks.Task<SseEvent?> ReadEventAsync(
        StreamReader reader,
        int maxLineBytes,
        int maxEventBytes,
        CancellationToken cancellationToken)
    {
        if (maxLineBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLineBytes));
        }

        if (maxEventBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEventBytes));
        }

        string? eventName = null;
        var data = new StringBuilder();
        string? id = null;
        var hasDataField = false;
        var eventBytes = 0;

        while (true)
        {
            var line = await ReadLineAsync(reader, maxLineBytes, cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                var dispatched = TryDispatch(hasDataField, eventName, data, id);
                if (dispatched is not null)
                {
                    return dispatched;
                }

                eventName = null;
                data.Clear();
                id = null;
                hasDataField = false;
                eventBytes = 0;
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var colon = line.IndexOf(':');
            string field;
            string value;
            if (colon < 0)
            {
                field = line;
                value = string.Empty;
            }
            else
            {
                field = line.Substring(0, colon);
                value = line.Substring(colon + 1);
                if (value.Length > 0 && value[0] == ' ')
                {
                    value = value.Substring(1);
                }
            }

            var lineBytes = Encoding.UTF8.GetByteCount(line);
            eventBytes += lineBytes + 1;
            if (eventBytes > maxEventBytes)
            {
                throw new InvalidOperationException($"SSE event exceeded the maximum size of {maxEventBytes} bytes.");
            }

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    hasDataField = true;
                    data.Append(value).Append('\n');
                    break;
                case "id":
                    id = value;
                    break;
            }
        }
    }

    /// <summary>
    /// Deserializes an SSE <c>data</c> payload into <typeparamref name="T"/>.
    /// String payloads pass through on every TFM. Other CLR shapes require net8.0 or later
    /// (the netstandard2.0 runtime throws <see cref="NotSupportedException"/>).
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(string data)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)data;
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Deserializing SSE payloads to types other than string requires net8.0 or later.");
#else
        var value = JsonSerializer.Deserialize<T>(data, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException("SSE payload deserialized to null.");
        }

        return value;
#endif
    }

    static SseEvent? TryDispatch(bool hasDataField, string? eventName, StringBuilder data, string? id)
    {
        if (!hasDataField)
        {
            return null;
        }

        var payload = data.ToString();
        if (payload.Length > 0 && payload[payload.Length - 1] == '\n')
        {
            payload = payload.Substring(0, payload.Length - 1);
        }

        return new SseEvent(string.IsNullOrWhiteSpace(eventName) ? "message" : eventName!, payload, id);
    }

    static async System.Threading.Tasks.Task<string?> ReadLineAsync(
        StreamReader reader,
        int maxLineBytes,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var buffer = new char[1];
        var lineBytes = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
            var n = await reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
#else
            var n = await reader.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
#endif
            if (n == 0)
            {
                return sb.Length == 0 ? null : sb.ToString();
            }

            var ch = buffer[0];
            if (ch == '\n')
            {
                if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                {
                    sb.Length--;
                }

                return sb.ToString();
            }

            sb.Append(ch);
            lineBytes += Encoding.UTF8.GetByteCount(buffer, 0, 1);
            if (lineBytes > maxLineBytes)
            {
                throw new InvalidOperationException($"SSE line exceeded the maximum size of {maxLineBytes} bytes.");
            }
        }
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload deserialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload deserialization uses System.Text.Json reflection.")]
#endif
    internal static async Task SubscribeAsync<T>(
        SseConnection connection,
        string eventName,
        Action<T> onNext,
        Action onCompleted,
        CancellationToken cancellationToken)
    {
        if (connection.MaxLineBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), connection.MaxLineBytes, "MaxLineBytes must be positive.");
        }

        if (connection.MaxEventBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(connection), connection.MaxEventBytes, "MaxEventBytes must be positive.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, connection.Endpoint);
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await connection.HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

#if NETSTANDARD2_0
        using var cancelReg = cancellationToken.Register(
            static state =>
            {
                try
                {
                    ((HttpResponseMessage)state!).Dispose();
                }
                catch (Exception)
                {
                    // best-effort unblock of ReadLineAsync
                }
            },
            response);
#endif

#if NET8_0_OR_GREATER
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SseEvent? sseEvent;
            try
            {
                sseEvent = await ReadEventAsync(
                        reader,
                        connection.MaxLineBytes,
                        connection.MaxEventBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (sseEvent is null)
            {
                onCompleted();
                return;
            }

            if (sseEvent.Value.EventName == eventName)
            {
                onNext(Deserialize<T>(sseEvent.Value.Data));
            }
        }
    }
}
