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
                observer.OnErrorResume(ex);
            }
        });
}
