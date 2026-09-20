using StackExchange.Redis;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Redis;

internal static class RedisProtocol
{
    internal static async Task PublishAsync(
        IConnectionMultiplexer multiplexer,
        string channel,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var subscriber = multiplexer.GetSubscriber();
        // ISubscriber.PublishAsync has no CancellationToken in StackExchange.Redis 2.8.x.
        // WaitAsync only abandons the local wait; the in-flight PUBLISH is not aborted.
        await subscriber
            .PublishAsync(RedisChannel.Literal(channel), payload ?? Array.Empty<byte>())
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    internal static T DeserializePayload<T>(ChannelMessage message) =>
        RedisPayloadSerializers.Deserialize<T>((byte[]?)message.Message ?? Array.Empty<byte>());

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    internal static RedisMessage<T> ToRedisMessage<T>(ChannelMessage message) =>
        new(message.Channel.ToString(), DeserializePayload<T>(message));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(RedisTrimAnnotations.JsonPayload)]
    [RequiresDynamicCode(RedisTrimAnnotations.JsonPayload)]
#endif
    internal static async Task SubscribeAsync<T>(
        IConnectionMultiplexer multiplexer,
        RedisChannel channel,
        Func<ChannelMessage, T> map,
        Action<T> onNext,
        Action onCompleted,
        CancellationToken cancellationToken)
    {
        ChannelMessageQueue? queue = null;
        Task<ChannelMessageQueue>? subscribeTask = null;
        try
        {
            var subscriber = multiplexer.GetSubscriber();
            subscribeTask = subscriber.SubscribeAsync(channel);
            queue = await subscribeTask.WaitAsync(cancellationToken).ConfigureAwait(false);

            // ChannelMessageQueue enumeration is sequential (SER OnMessage / queue path).
            await foreach (var message in queue.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                onNext(map(message));
            }

            onCompleted();
        }
        finally
        {
            await UnsubscribeBestEffortAsync(queue, subscribeTask).ConfigureAwait(false);
        }
    }

    static async Task UnsubscribeBestEffortAsync(
        ChannelMessageQueue? queue,
        Task<ChannelMessageQueue>? subscribeTask)
    {
        if (queue is null && subscribeTask is not null)
        {
            try
            {
                queue = await subscribeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }
        }

        if (queue is null)
        {
            return;
        }

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
