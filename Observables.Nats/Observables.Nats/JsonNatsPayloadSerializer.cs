#if !NETSTANDARD2_0
using System.Text;
using System.Text.Json;

namespace Observables.Nats;

/// <summary>UTF-8 JSON serializer for complex MQTT payloads (net8.0+).</summary>
public sealed class JsonNatsPayloadSerializer : INatsPayloadSerializer
{
    public static JsonNatsPayloadSerializer Instance { get; } = new();

    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    JsonNatsPayloadSerializer()
    {
    }

    public object Deserialize(Type payloadType, ReadOnlySpan<byte> payload)
    {
        var json = Encoding.UTF8.GetString(payload);
        var value = JsonSerializer.Deserialize(json, payloadType, DefaultOptions);
        if (value is null)
        {
            throw new InvalidOperationException("MQTT payload deserialized to null.");
        }

        return value;
    }

    public byte[] Serialize(Type payloadType, object? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, payloadType, DefaultOptions));
    }
}
#endif
