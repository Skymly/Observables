namespace Observables.Postgres;

/// <summary>Typed PostgreSQL payload serializer for a single CLR type.</summary>
public interface IPostgresPayloadSerializer<T>
{
    /// <summary>Deserializes a payload to <typeparamref name="T"/>.</summary>
    T Deserialize(ReadOnlySpan<byte> payload);

    /// <summary>Serializes <paramref name="value"/> to a wire payload.</summary>
    byte[] Serialize(T value);
}
