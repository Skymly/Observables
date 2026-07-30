using System.Diagnostics.CodeAnalysis;

namespace Observables.Postgres;

/// <summary>Converts PostgreSQL NOTIFY payloads to and from CLR values.</summary>
/// <remarks>
/// The default implementation supports UTF-8 <see cref="string"/> payloads and JSON for other types.
/// Register a custom IPostgresPayloadSerializer via <see cref="PostgresPayloadSerializers.Current"/>,
/// or a typed IPostgresPayloadSerializer{T} via <see cref="PostgresPayloadSerializers.Register{T}(IPostgresPayloadSerializer{T})"/>.
/// Convenience overloads: <see cref="PostgresPayloadSerializerExtensions"/>.
/// </remarks>
public interface IPostgresPayloadSerializer
{
    /// <summary>Deserializes a payload to <paramref name="payloadType"/>.</summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    [RequiresUnreferencedCode(PostgresTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(PostgresTrimAnnotations.JsonPayload)]
    byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value);
}
