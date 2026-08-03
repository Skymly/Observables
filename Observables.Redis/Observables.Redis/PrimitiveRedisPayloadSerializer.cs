using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Observables.Redis;

/// <summary>Built-in serializer for raw <see cref="byte"/>[] and UTF-8 <see cref="string"/> payloads.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
public sealed class PrimitiveRedisPayloadSerializer : IRedisPayloadSerializer
{
    public static PrimitiveRedisPayloadSerializer Instance { get; } = new();

    PrimitiveRedisPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        if (payloadType == typeof(byte[]))
        {
            return payload.ToArray();
        }

        if (payloadType == typeof(string))
        {
            return Encoding.UTF8.GetString(payload);
        }

        throw CreateUnsupportedException(payloadType, deserialize: true);
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (payloadType == typeof(byte[]))
        {
            return value is byte[] bytes ? bytes : Array.Empty<byte>();
        }

        if (payloadType == typeof(string))
        {
            return Encoding.UTF8.GetBytes(value as string ?? string.Empty);
        }

        throw CreateUnsupportedException(payloadType, deserialize: false);
    }

    static NotSupportedException CreateUnsupportedException(Type payloadType, bool deserialize)
    {
        var direction = deserialize ? "Deserialize" : "Serialize";
        return new NotSupportedException(
            $"{direction} for Redis payload type '{payloadType.FullName}' is not supported by the built-in serializer. "
            + "Register IRedisPayloadSerializer<T> via RedisPayloadSerializers.Register<T>, "
            + "or assign a custom IRedisPayloadSerializer to RedisPayloadSerializers.Current.");
    }
}
