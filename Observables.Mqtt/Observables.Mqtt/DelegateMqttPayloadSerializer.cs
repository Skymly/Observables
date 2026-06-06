namespace Observables.Mqtt;

internal sealed class DelegateMqttPayloadSerializer<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize)
    : IMqttPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) => deserialize(payload.ToArray());

    public byte[] Serialize(T value) => serialize(value);
}
