namespace Observables.Nats;

internal sealed class DelegateNatsPayloadSerializer<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize)
    : INatsPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) => deserialize(payload.ToArray());

    public byte[] Serialize(T value) => serialize(value);
}
