using System.Collections.Concurrent;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

internal static class MqttProtocol
{
    static readonly ConcurrentDictionary<FilterKey, FilterGate> Filters = new();

    internal static async Task PublishAsync(
        IMqttClient client,
        string topic,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload ?? Array.Empty<byte>())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        var result = await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"MQTT publish rejected: {result.ReasonCode}");
        }
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    internal static T DeserializePayload<T>(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = e.ApplicationMessage.PayloadSegment.Count == 0
            ? Array.Empty<byte>()
            : e.ApplicationMessage.PayloadSegment.ToArray();
        return MqttPayloadSerializers.Deserialize<T>(payload);
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    internal static async Task SubscribeAsync<T>(
        IMqttClient client,
        string topicFilter,
        Action<T> onNext,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        async Task Handler(MqttApplicationMessageReceivedEventArgs e)
        {
            if (!MqttTopicMatcher.Matches(topicFilter, e.ApplicationMessage.Topic))
            {
                return;
            }

            try
            {
                onNext(DeserializePayload<T>(e));
            }
            catch (Exception ex)
            {
                onError(ex);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        var key = new FilterKey(client, topicFilter);
        client.ApplicationMessageReceivedAsync += Handler;
        try
        {
            await AddSubscriberAsync(key, client, topicFilter).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            client.ApplicationMessageReceivedAsync -= Handler;
            await RemoveSubscriberAsync(key, client, topicFilter).ConfigureAwait(false);
        }
    }

    static Task AddSubscriberAsync(FilterKey key, IMqttClient client, string topicFilter)
    {
        while (true)
        {
            var gate = Filters.GetOrAdd(key, static _ => new FilterGate());
            lock (gate.Sync)
            {
                if (gate.Removed)
                {
                    continue;
                }

                gate.Count++;
                if (gate.Count == 1)
                {
                    gate.BrokerWork = ContinueSubscribe(gate.BrokerWork, client, topicFilter);
                }

                return gate.BrokerWork;
            }
        }
    }

    static Task RemoveSubscriberAsync(FilterKey key, IMqttClient client, string topicFilter)
    {
        if (!Filters.TryGetValue(key, out var gate))
        {
            return Task.CompletedTask;
        }

        lock (gate.Sync)
        {
            gate.Count--;
            if (gate.Count > 0)
            {
                return Task.CompletedTask;
            }

            gate.Count = 0;
            gate.BrokerWork = ContinueUnsubscribe(gate.BrokerWork, client, topicFilter, key, gate);
            return gate.BrokerWork;
        }
    }

    static async Task ContinueSubscribe(Task previous, IMqttClient client, string topicFilter)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A failed previous generation must not block a new subscribe.
        }

        await SubscribeBrokerAsync(client, topicFilter, CancellationToken.None).ConfigureAwait(false);
    }

    static async Task ContinueUnsubscribe(
        Task previous,
        IMqttClient client,
        string topicFilter,
        FilterKey key,
        FilterGate gate)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // best-effort if subscribe never completed
        }

        lock (gate.Sync)
        {
            if (gate.Count > 0)
            {
                return;
            }
        }

        try
        {
            using var unsubCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.UnsubscribeAsync(topicFilter, unsubCts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // best-effort if the client is already down
        }
        finally
        {
            lock (gate.Sync)
            {
                if (gate.Count == 0)
                {
                    gate.Removed = true;
                    Filters.TryRemove(key, out _);
                }
            }
        }
    }

    static async Task SubscribeBrokerAsync(
        IMqttClient client,
        string topicFilter,
        CancellationToken cancellationToken)
    {
        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(new MqttTopicFilterBuilder().WithTopic(topicFilter).Build())
            .Build();
        var result = await client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);
        EnsureGranted(result);
    }

    static void EnsureGranted(MqttClientSubscribeResult result)
    {
        foreach (var item in result.Items)
        {
            if (item.ResultCode is not (
                MqttClientSubscribeResultCode.GrantedQoS0 or
                MqttClientSubscribeResultCode.GrantedQoS1 or
                MqttClientSubscribeResultCode.GrantedQoS2))
            {
                throw new InvalidOperationException($"MQTT subscribe rejected: {item.ResultCode}");
            }
        }
    }

    readonly record struct FilterKey(IMqttClient Client, string TopicFilter);

    sealed class FilterGate
    {
        public readonly object Sync = new();
        public int Count;
        public bool Removed;
        public Task BrokerWork = Task.CompletedTask;
    }
}
