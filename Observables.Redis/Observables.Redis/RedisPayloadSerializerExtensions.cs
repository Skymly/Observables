using System.Diagnostics.CodeAnalysis;

namespace Observables.Redis;

/// <summary>Convenience overloads for <see cref="IRedisPayloadSerializer"/> and <see cref="IRedisPayloadSerializer{T}"/>.</summary>
public static class RedisPayloadSerializerExtensions
{
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static object Deserialize(
        this IRedisPayloadSerializer serializer,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        byte[] payload) =>
        serializer.Deserialize(payloadType, (ReadOnlySpan<byte>)payload);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(this IRedisPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static T Deserialize<T>(this IRedisPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static byte[] Serialize<T>(this IRedisPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    public static T Deserialize<T>(this IRedisPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
