using R3;
using StackExchange.Redis;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Redis;

/// <summary>Bridges StackExchange.Redis Pub/Sub APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class RedisObservable
{
    public static Observable<Unit> FromPublish(
        IConnectionMultiplexer multiplexer,
        string channel,
        CancellationToken cancellationToken = default) =>
        FromPublish(multiplexer, channel, Array.Empty<byte>(), cancellationToken);

    /// <summary>Publishes <paramref name="payload"/> on <paramref name="channel"/>.</summary>
    /// <remarks>
    /// <paramref name="cancellationToken"/> cancels the local wait for the StackExchange.Redis
    /// publish task. <c>ISubscriber.PublishAsync</c> has no cancellation-token overload in 2.8.x,
    /// so an in-flight <c>PUBLISH</c> is not aborted.
    /// </remarks>
    public static Observable<Unit> FromPublish(
        IConnectionMultiplexer multiplexer,
        string channel,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await RedisProtocol.PublishAsync(multiplexer, channel, payload, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

    /// <summary>Publishes the serialized <paramref name="payload"/> on <paramref name="channel"/>.</summary>
    /// <remarks>
    /// <paramref name="payload"/> is serialized when this method is called, not when the observable
    /// is subscribed. The captured byte array is reused for every subscription.
    /// <paramref name="cancellationToken"/> cancels the local wait for the StackExchange.Redis
    /// publish task. <c>ISubscriber.PublishAsync</c> has no cancellation-token overload in 2.8.x,
    /// so an in-flight <c>PUBLISH</c> is not aborted.
    /// </remarks>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<Unit> FromPublish<T>(
        IConnectionMultiplexer multiplexer,
        string channel,
        T payload,
        CancellationToken cancellationToken = default) =>
        FromPublish(multiplexer, channel, RedisPayloadSerializers.Serialize(payload), cancellationToken);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<T> FromSubscribe<T>(IConnectionMultiplexer multiplexer, string channel) =>
        CreateSubscribe(multiplexer, RedisChannel.Literal(channel), static message => RedisProtocol.DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<T> FromPatternSubscribe<T>(IConnectionMultiplexer multiplexer, string pattern) =>
        CreateSubscribe(multiplexer, RedisChannel.Pattern(pattern), static message => RedisProtocol.DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<RedisMessage<T>> FromSubscribeMessage<T>(
        IConnectionMultiplexer multiplexer,
        string channel) =>
        CreateSubscribe(
            multiplexer,
            RedisChannel.Literal(channel),
            static message => RedisProtocol.ToRedisMessage<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<RedisMessage<T>> FromPatternSubscribeMessage<T>(
        IConnectionMultiplexer multiplexer,
        string pattern) =>
        CreateSubscribe(
            multiplexer,
            RedisChannel.Pattern(pattern),
            static message => RedisProtocol.ToRedisMessage<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    static Observable<T> CreateSubscribe<T>(
        IConnectionMultiplexer multiplexer,
        RedisChannel channel,
        Func<ChannelMessage, T> map) =>
        R3AsyncCreate.Create<T>(async (observer, ct) =>
        {
            await RedisProtocol
                .SubscribeAsync(multiplexer, channel, map, observer.OnNext, observer.OnCompleted, ct)
                .ConfigureAwait(false);
        });
}
