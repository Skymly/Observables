using System.Diagnostics.CodeAnalysis;

namespace Observables.Postgres;

/// <summary>
/// Default serializer: UTF-8 <see cref="string"/> via
/// <see cref="PrimitivePostgresPayloadSerializer"/>; JSON for other types.
/// </summary>
[RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
public sealed class DefaultPostgresPayloadSerializer : IPostgresPayloadSerializer
{
    public static DefaultPostgresPayloadSerializer Instance { get; } = new();

    DefaultPostgresPayloadSerializer()
    {
    }

    public object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload)
    {
        if (payloadType == typeof(string))
        {
            return PrimitivePostgresPayloadSerializer.Instance.Deserialize(payloadType, payload);
        }

        return JsonPostgresPayloadSerializer.Instance.Deserialize(payloadType, payload);
    }

    public byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value)
    {
        if (payloadType == typeof(string))
        {
            return PrimitivePostgresPayloadSerializer.Instance.Serialize(payloadType, value);
        }

        return JsonPostgresPayloadSerializer.Instance.Serialize(payloadType, value);
    }
}
