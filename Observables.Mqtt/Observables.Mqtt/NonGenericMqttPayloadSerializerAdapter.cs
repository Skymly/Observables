namespace Observables.Mqtt;

internal sealed class NonGenericMqttPayloadSerializerAdapter<T>(IMqttPayloadSerializer serializer)
    : IMqttPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
