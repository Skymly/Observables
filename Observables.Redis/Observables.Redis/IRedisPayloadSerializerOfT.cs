namespace Observables.Redis;

/// <summary>Typed Redis payload serializer for a single CLR type.</summary>
public interface IRedisPayloadSerializer<T>
{
    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    T Deserialize(ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    byte[] Serialize(T value);
}
