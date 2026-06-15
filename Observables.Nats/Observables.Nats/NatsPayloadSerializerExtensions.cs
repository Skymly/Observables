using System.Diagnostics.CodeAnalysis;

namespace Observables.Nats;

/// <summary>Convenience overloads for <see cref="INatsPayloadSerializer"/> and <see cref="INatsPayloadSerializer{T}"/>.</summary>
public static class NatsPayloadSerializerExtensions
{
    /// <summary>Deserializes a payload buffer to <paramref name="payloadType"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(NatsTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(NatsTrimAnnotations.JsonPayload)]
#endif
    public static object Deserialize(
        this INatsPayloadSerializer serializer,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        byte[] payload) =>
        serializer.Deserialize(payloadType, (ReadOnlySpan<byte>)payload);

    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(this INatsPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(this INatsPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static byte[] Serialize<T>(this INatsPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this INatsPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
