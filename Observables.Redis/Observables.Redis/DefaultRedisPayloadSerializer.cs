using System.Diagnostics.CodeAnalysis;

namespace Observables.Redis;

/// <summary>
/// Default serializer: raw <see cref="byte"/>[] / UTF-8 <see cref="string"/> via
/// <see cref="PrimitiveRedisPayloadSerializer"/>; JSON for other types on net8.0+.
/// </summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
public sealed class DefaultRedisPayloadSerializer : IRedisPayloadSerializer
{
    public static DefaultRedisPayloadSerializer Instance { get; } = new();

    DefaultRedisPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        if (payloadType == typeof(byte[]) || payloadType == typeof(string))
        {
            return PrimitiveRedisPayloadSerializer.Instance.Deserialize(payloadType, payload);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Deserializing Redis payloads to types other than byte[] or string requires net8.0 or later.");
#else
        return JsonRedisPayloadSerializer.Instance.Deserialize(payloadType, payload);
#endif
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (payloadType == typeof(byte[]) || payloadType == typeof(string))
        {
            return PrimitiveRedisPayloadSerializer.Instance.Serialize(payloadType, value);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Serializing Redis payloads for types other than byte[] or string requires net8.0 or later.");
#else
        return JsonRedisPayloadSerializer.Instance.Serialize(payloadType, value);
#endif
    }
}
