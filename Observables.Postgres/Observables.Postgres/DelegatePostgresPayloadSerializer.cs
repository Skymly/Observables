namespace Observables.Postgres;

internal sealed class DelegatePostgresPayloadSerializer<T>(Func<byte[], T> deserialize, Func<T, byte[]> serialize)
    : IPostgresPayloadSerializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> payload) => deserialize(payload.ToArray());

    public byte[] Serialize(T value) => serialize(value);
}
