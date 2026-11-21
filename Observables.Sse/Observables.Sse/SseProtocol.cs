using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
    static readonly ConditionalWeakTable<StreamReader, LineReader> LineReaders = new();

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
        long eventBytes = 0;
        var lineReader = LineReaders.GetValue(reader, static _ => new LineReader());

        while (true)
        {
            var read = await lineReader.ReadAsync(reader, maxLineBytes, cancellationToken).ConfigureAwait(false);

            if (read is not { } lineResult)
            {
                return null;
            }

            var (line, lineBytes) = lineResult;
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

            eventBytes += lineBytes + 1L;
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

    sealed class LineReader
    {
        readonly char[] buffer = new char[1];
        bool skipLineFeed;

        internal async Task<(string Line, int Bytes)?> ReadAsync(
            StreamReader reader, int maxLineBytes, CancellationToken cancellationToken)
        {
            var line = new StringBuilder();
            int lineBytes = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
#if NET8_0_OR_GREATER
                int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
                int read = await reader.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
#endif
                if (read == 0)
                {
                    return line.Length == 0 ? null : (line.ToString(), lineBytes);
                }

                char ch = buffer[0];
                if (skipLineFeed)
                {
                    skipLineFeed = false;
                    if (ch == '\n')
                    {
                        continue;
                    }
                }

                if (ch is '\r' or '\n')
                {
                    // Return immediately on CR. An optional LF belongs to this terminator,
                    // including when the next read is for a different event.
                    skipLineFeed = ch == '\r';
                    return (line.ToString(), lineBytes);
                }

                // A high surrogate already contributes three bytes. Its matching low surrogate
                // brings the pair to four. An unpaired surrogate counts as a replacement character.
                int charBytes = ch switch
                {
                    <= '\u007F' => 1,
                    <= '\u07FF' => 2,
                    _ when char.IsLowSurrogate(ch) && line.Length > 0 && char.IsHighSurrogate(line[line.Length - 1]) => 1,
                    _ => 3,
                };
                if (charBytes > maxLineBytes - lineBytes)
                {
                    throw new InvalidOperationException($"SSE line exceeded the maximum size of {maxLineBytes} bytes.");
                }

                line.Append(ch);
                lineBytes += charBytes;
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
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            onCompleted();
            return;
        }

        var content = response.Content;
        if (content is null
            || !string.Equals(content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Expected a text/event-stream response from the SSE endpoint.");
        }

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
                    // best-effort unblock of line reading
                }
            },
            response);
#endif

#if NET8_0_OR_GREATER
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
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
