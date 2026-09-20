using System.Reactive.Linq;
using Observables.Redis;
using StackExchange.Redis;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Redis.Reactive;

/// <summary>Bridges StackExchange.Redis Pub/Sub APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveRedisAdapter
{
    public static IObservable<System.Reactive.Unit> FromPublish(
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
    public static IObservable<System.Reactive.Unit> FromPublish(
        IConnectionMultiplexer multiplexer,
        string channel,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await RedisProtocol.PublishAsync(multiplexer, channel, payload, linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
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
    public static IObservable<System.Reactive.Unit> FromPublish<T>(
        IConnectionMultiplexer multiplexer,
        string channel,
        T payload,
        CancellationToken cancellationToken = default) =>
        FromPublish(multiplexer, channel, RedisPayloadSerializers.Serialize(payload), cancellationToken);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static IObservable<T> FromSubscribe<T>(IConnectionMultiplexer multiplexer, string channel) =>
        CreateSubscribe(multiplexer, RedisChannel.Literal(channel), static message => RedisProtocol.DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static IObservable<T> FromPatternSubscribe<T>(IConnectionMultiplexer multiplexer, string pattern) =>
        CreateSubscribe(multiplexer, RedisChannel.Pattern(pattern), static message => RedisProtocol.DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static IObservable<RedisMessage<T>> FromSubscribeMessage<T>(
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
    public static IObservable<RedisMessage<T>> FromPatternSubscribeMessage<T>(
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
    static IObservable<T> CreateSubscribe<T>(
        IConnectionMultiplexer multiplexer,
        RedisChannel channel,
        Func<ChannelMessage, T> map) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await RedisProtocol
                    .SubscribeAsync(multiplexer, channel, map, observer.OnNext, observer.OnCompleted, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnError(ex);
            }
        });
}
