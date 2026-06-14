namespace Observables.Nats;

/// <summary>Converts MQTT application message payloads to and from CLR values.</summary>
/// <remarks>
/// The default implementation supports <see cref="byte"/>[] and <see cref="string"/> only.
/// Register a custom INatsPayloadSerializer via <see cref="NatsPayloadSerializers.Current"/>,
/// or a typed INatsPayloadSerializer{T} via <see cref="NatsPayloadSerializers.Register{T}(INatsPayloadSerializer{T})"/>.
/// Convenience overloads: <see cref="NatsPayloadSerializerExtensions"/>.
/// </remarks>
public interface INatsPayloadSerializer
{
    /// <summary>Deserializes a payload to <paramref name="payloadType"/>.</summary>
    object Deserialize(Type payloadType, ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    byte[] Serialize(Type payloadType, object? value);
}
