namespace Observables.Redis;

internal sealed class DelegateRedisPayloadSerializer<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize)
    : IRedisPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) => deserialize(payload.ToArray());

    public byte[] Serialize(T value) => serialize(value);
}
