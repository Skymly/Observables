using System.Diagnostics.CodeAnalysis;

namespace Observables.Nats;

/// <summary>Converts NATS message payloads to and from CLR values.</summary>
/// <remarks>
/// The default implementation supports <see cref="byte"/>[] and <see cref="string"/> only.
/// Register a custom INatsPayloadSerializer via <see cref="NatsPayloadSerializers.Current"/>,
/// or a typed INatsPayloadSerializer{T} via <see cref="NatsPayloadSerializers.Register{T}(INatsPayloadSerializer{T})"/>.
/// Convenience overloads: <see cref="NatsPayloadSerializerExtensions"/>.
/// </remarks>
public interface INatsPayloadSerializer
{
    /// <summary>Deserializes a payload to <paramref name="payloadType"/>.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    object Deserialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    byte[] Serialize(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type payloadType,
        object? value);
}
