using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Observables.Postgres;

/// <summary>Built-in serializer for UTF-8 <see cref="string"/> payloads.</summary>
[RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
public sealed class PrimitivePostgresPayloadSerializer : IPostgresPayloadSerializer
{
    public static PrimitivePostgresPayloadSerializer Instance { get; } = new();

    PrimitivePostgresPayloadSerializer()
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
            $"{direction} for PostgreSQL payload type '{payloadType.FullName}' is not supported by the built-in serializer. "
            + "Register IPostgresPayloadSerializer<T> via PostgresPayloadSerializers.Register<T>, "
            + "or assign a custom IPostgresPayloadSerializer to PostgresPayloadSerializers.Current "
            + "(for example one that wraps System.Text.Json, Newtonsoft.Json, or Protobuf).");
    }
}
