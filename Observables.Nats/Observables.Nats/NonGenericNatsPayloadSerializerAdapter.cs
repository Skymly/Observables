namespace Observables.Nats;

internal sealed class NonGenericNatsPayloadSerializerAdapter<T>(INatsPayloadSerializer serializer)
    : INatsPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) =>
        (T)serializer.Deserialize(typeof(T), payload);

    public byte[] Serialize(T value) => serializer.Serialize(typeof(T), value);
}
