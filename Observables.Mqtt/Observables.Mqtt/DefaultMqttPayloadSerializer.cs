using System.Diagnostics.CodeAnalysis;

namespace Observables.Mqtt;

/// <summary>
/// Default serializer: raw <see cref="byte"/>[] / UTF-8 <see cref="string"/> via
/// <see cref="PrimitiveMqttPayloadSerializer"/>; JSON for other types on net8.0+.
/// </summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
public sealed class DefaultMqttPayloadSerializer : IMqttPayloadSerializer
{
    public static DefaultMqttPayloadSerializer Instance { get; } = new();

    DefaultMqttPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        if (payloadType == typeof(byte[]) || payloadType == typeof(string))
        {
            return PrimitiveMqttPayloadSerializer.Instance.Deserialize(payloadType, payload);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Deserializing MQTT payloads to types other than byte[] or string requires net8.0 or later.");
#else
        return JsonMqttPayloadSerializer.Instance.Deserialize(payloadType, payload);
#endif
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (payloadType == typeof(byte[]) || payloadType == typeof(string))
        {
            return PrimitiveMqttPayloadSerializer.Instance.Serialize(payloadType, value);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Serializing MQTT payloads for types other than byte[] or string requires net8.0 or later.");
#else
        return JsonMqttPayloadSerializer.Instance.Serialize(payloadType, value);
#endif
    }
}
