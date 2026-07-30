using System.Diagnostics.CodeAnalysis;

namespace Observables.Postgres;

/// <summary>Convenience overloads for <see cref="IPostgresPayloadSerializer"/> and <see cref="IPostgresPayloadSerializer{T}"/>.</summary>
public static class PostgresPayloadSerializerExtensions
{
    /// <summary>Deserializes a payload buffer to <paramref name="payloadType"/>.</summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    public static object Deserialize(
        this IPostgresPayloadSerializer serializer,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        byte[] payload) =>
        serializer.Deserialize(payloadType, (ReadOnlySpan<byte>)payload);

    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
    public static T Deserialize<T>(this IPostgresPayloadSerializer serializer, ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
    public static T Deserialize<T>(this IPostgresPayloadSerializer serializer, byte[] payload) =>
        serializer.Deserialize<T>((ReadOnlySpan<byte>)payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
    public static byte[] Serialize<T>(this IPostgresPayloadSerializer serializer, T value) =>
        serializer.Serialize(typeof(T), value);

    /// <summary>Deserializes a payload buffer to <typeparamref name="T"/>.</summary>
    public static T Deserialize<T>(this IPostgresPayloadSerializer<T> serializer, byte[] payload) =>
        serializer.Deserialize((ReadOnlySpan<byte>)payload);
}
