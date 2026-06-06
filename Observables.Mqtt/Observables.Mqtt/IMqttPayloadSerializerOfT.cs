namespace Observables.Mqtt;

/// <summary>Typed MQTT payload serializer for a single CLR type.</summary>
public interface IMqttPayloadSerializer<T>
{
    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    T Deserialize(ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    byte[] Serialize(T value);
}
