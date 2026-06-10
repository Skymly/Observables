using System.IO;
using System.Text;
#if NETSTANDARD2_0
#else
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

    /// <summary>The event type ("message" when the wire <c>event</c> field is absent).</summary>
    public string EventName { get; }

    /// <summary>The concatenated <c>data</c> payload (without the trailing newline).</summary>
    public string Data { get; }

    /// <summary>The last <c>id</c> field, if any.</summary>
    public string? Id { get; }
}

/// <summary>
/// Minimal <c>text/event-stream</c> parser shared by the R3 and System.Reactive SSE bridges.
/// Follows the WHATWG SSE field grammar (event / data / id / comment).
/// </summary>
public static class SseProtocol
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

    /// <summary>Reads the next dispatched event from the stream, or null at end of stream.</summary>
    public static async System.Threading.Tasks.Task<SseEvent?> ReadEventAsync(StreamReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        string? eventName = null;
        var data = new StringBuilder();
        string? id = null;
        var hasFields = false;

        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);

            if (line is null)
            {
                return hasFields ? Build(eventName, data, id) : (SseEvent?)null;
            }

            if (line.Length == 0)
            {
                if (hasFields)
                {
                    return Build(eventName, data, id);
                }

                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            hasFields = true;
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

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    data.Append(value).Append('\n');
                    break;
                case "id":
                    id = value;
                    break;
            }
        }
    }

    /// <summary>Deserializes an SSE <c>data</c> payload into <typeparamref name="T"/>.</summary>
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

    static SseEvent Build(string? eventName, StringBuilder data, string? id)
    {
        var payload = data.ToString();
        if (payload.Length > 0 && payload[payload.Length - 1] == '\n')
        {
            payload = payload.Substring(0, payload.Length - 1);
        }

        return new SseEvent(string.IsNullOrEmpty(eventName) ? "message" : eventName!, payload, id);
    }
}
