namespace Observables.Mqtt;

/// <summary>Convenience overloads for <see cref="IMqttPayloadSerializer"/> and <see cref="IMqttPayloadSerializer{T}"/>.</summary>
public static class MqttPayloadSerializerExtensions
{
    /// <summary>Deserializes a payload buffer to <paramref name="payloadType"/>.</summary>
    public static object Deserialize(this IMqttPayloadSerializer serializer, Type payloadType, byte[] payload) =>
        serializer.Deserialize(payloadType, (ReadOnlySpan<byte>)payload);

    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this IMqttPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this IMqttPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    public static byte[] Serialize<T>(this IMqttPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this IMqttPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
