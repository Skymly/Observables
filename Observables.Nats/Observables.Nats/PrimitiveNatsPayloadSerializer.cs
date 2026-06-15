using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Observables.Nats;

/// <summary>Built-in serializer for raw <see cref="byte"/>[] and UTF-8 <see cref="string"/> payloads.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(NatsTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(NatsTrimAnnotations.JsonPayload)]
#endif
public sealed class PrimitiveNatsPayloadSerializer : INatsPayloadSerializer
{
    public static PrimitiveNatsPayloadSerializer Instance { get; } = new();

    PrimitiveNatsPayloadSerializer()
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
            $"{direction} for NATS payload type '{payloadType.FullName}' is not supported by the built-in serializer. "
            + "Register INatsPayloadSerializer<T> via NatsPayloadSerializers.Register<T>, "
            + "or assign a custom INatsPayloadSerializer to NatsPayloadSerializers.Current "
            + "(for example one that wraps System.Text.Json, Newtonsoft.Json, or Protobuf).");
    }
}
