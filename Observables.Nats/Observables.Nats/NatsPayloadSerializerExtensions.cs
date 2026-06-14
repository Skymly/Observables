namespace Observables.Nats;

/// <summary>Convenience overloads for <see cref="INatsPayloadSerializer"/> and <see cref="INatsPayloadSerializer{T}"/>.</summary>
public static class NatsPayloadSerializerExtensions
{
    /// <summary>Deserializes a payload buffer to <paramref name="payloadType"/>.</summary>
    public static object Deserialize(this INatsPayloadSerializer serializer, Type payloadType, byte[] payload) =>
        serializer.Deserialize(payloadType, (ReadOnlySpan<byte>)payload);

    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this INatsPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this INatsPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    public static byte[] Serialize<T>(this INatsPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this INatsPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
