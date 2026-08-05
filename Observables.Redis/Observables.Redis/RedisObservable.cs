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
            linked.Token.ThrowIfCancellationRequested();
            var subscriber = multiplexer.GetSubscriber();
            await subscriber
                .PublishAsync(RedisChannel.Literal(channel), payload ?? Array.Empty<byte>())
                .ConfigureAwait(false);
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
        CreateSubscribe(multiplexer, RedisChannel.Literal(channel), static message => DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    public static Observable<T> FromPatternSubscribe<T>(IConnectionMultiplexer multiplexer, string pattern) =>
        CreateSubscribe(multiplexer, RedisChannel.Pattern(pattern), static message => DeserializePayload<T>(message));

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
            static message => ToRedisMessage<T>(message));

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
            static message => ToRedisMessage<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    static T DeserializePayload<T>(ChannelMessage message) =>
        RedisPayload.Deserialize<T>((byte[]?)message.Message ?? Array.Empty<byte>());

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    static RedisMessage<T> ToRedisMessage<T>(ChannelMessage message) =>
        new(message.Channel.ToString(), DeserializePayload<T>(message));

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
            ChannelMessageQueue? queue = null;
            try
            {
                var subscriber = multiplexer.GetSubscriber();
                queue = await subscriber.SubscribeAsync(channel).ConfigureAwait(false);

                // ChannelMessageQueue enumeration is sequential (SER OnMessage / queue path).
                await foreach (var message in queue.WithCancellation(ct).ConfigureAwait(false))
                {
                    observer.OnNext(map(message));
                }

                observer.OnCompleted();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnErrorResume(ex);
            }
            finally
            {
                if (queue is not null)
                {
                    try
                    {
                        await queue.UnsubscribeAsync().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // best-effort unsubscribe on dispose / fault
                    }
                }
            }
        });
}
