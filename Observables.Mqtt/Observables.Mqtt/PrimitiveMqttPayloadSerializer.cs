using System.Text;

namespace Observables.Mqtt;

/// <summary>Built-in serializer for raw <see cref="byte"/>[] and UTF-8 <see cref="string"/> payloads.</summary>
public sealed class PrimitiveMqttPayloadSerializer : IMqttPayloadSerializer
{
    public static PrimitiveMqttPayloadSerializer Instance { get; } = new();

    PrimitiveMqttPayloadSerializer()
    {
    }

    public object Deserialize(Type payloadType, ReadOnlySpan<byte> payload)
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

    public byte[] Serialize(Type payloadType, object? value)
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
            $"{direction} for MQTT payload type '{payloadType.FullName}' is not supported by the built-in serializer. "
            + "Register IMqttPayloadSerializer<T> via MqttPayloadSerializers.Register<T>, "
            + "or assign a custom IMqttPayloadSerializer to MqttPayloadSerializers.Current "
            + "(for example one that wraps System.Text.Json, Newtonsoft.Json, or Protobuf).");
    }
}
