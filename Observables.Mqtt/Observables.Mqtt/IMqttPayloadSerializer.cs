using System.Diagnostics.CodeAnalysis;

namespace Observables.Mqtt;

/// <summary>Converts MQTT application message payloads to and from CLR values.</summary>
/// <remarks>
/// The default implementation supports <see cref="byte"/>[] and <see cref="string"/> only.
/// Register a custom IMqttPayloadSerializer via <see cref="MqttPayloadSerializers.Current"/>,
/// or a typed IMqttPayloadSerializer{T} via <see cref="MqttPayloadSerializers.Register{T}(IMqttPayloadSerializer{T})"/>.
/// Convenience overloads: <see cref="MqttPayloadSerializerExtensions"/>.
/// </remarks>
public interface IMqttPayloadSerializer
{
    /// <summary>Deserializes a payload to <paramref name="payloadType"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value);
}
