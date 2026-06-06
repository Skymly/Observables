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
    object Deserialize(Type payloadType, ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    byte[] Serialize(Type payloadType, object? value);
}
