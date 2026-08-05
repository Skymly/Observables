namespace Observables.Redis;

/// <summary>
/// Envelope for Redis Pub/Sub delivery: the concrete Channel that matched plus the typed payload.
/// Selected by returning <c>Observable&lt;RedisMessage&lt;T&gt;&gt;</c> (or <c>IObservable&lt;RedisMessage&lt;T&gt;&gt;</c>) from a subscribe property.
/// </summary>
public sealed class RedisMessage<T>
{
    public RedisMessage(string channel, T payload)
    {
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        Payload = payload;
    }

    /// <summary>Concrete Channel that delivered the message (not the Pattern template).</summary>
    public string Channel { get; }

    /// <summary>Deserialized Pub/Sub payload.</summary>
    public T Payload { get; }
}
