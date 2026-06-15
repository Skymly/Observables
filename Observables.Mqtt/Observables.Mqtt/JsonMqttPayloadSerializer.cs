#if !NETSTANDARD2_0
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Observables.Mqtt;

/// <summary>UTF-8 JSON serializer for complex MQTT payloads (net8.0+).</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
public sealed class JsonMqttPayloadSerializer : IMqttPayloadSerializer
{
    public static JsonMqttPayloadSerializer Instance { get; } = new();

    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    JsonMqttPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        var json = Encoding.UTF8.GetString(payload);
        var value = JsonSerializer.Deserialize(json, payloadType, DefaultOptions);
        if (value is null)
        {
            throw new InvalidOperationException("MQTT payload deserialized to null.");
        }

        return value;
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, payloadType, DefaultOptions));
    }
}
#endif
