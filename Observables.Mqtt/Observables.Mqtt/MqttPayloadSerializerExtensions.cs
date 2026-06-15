using System.Diagnostics.CodeAnalysis;

namespace Observables.Mqtt;

/// <summary>Convenience overloads for <see cref="IMqttPayloadSerializer"/> and <see cref="IMqttPayloadSerializer{T}"/>.</summary>
public static class MqttPayloadSerializerExtensions
{
    /// <summary>Deserializes a payload buffer to <paramref name="payloadType"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
    public static object Deserialize(
        this IMqttPayloadSerializer serializer,
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
    public static T Deserialize<T>(this IMqttPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(this IMqttPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static byte[] Serialize<T>(this IMqttPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this IMqttPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
