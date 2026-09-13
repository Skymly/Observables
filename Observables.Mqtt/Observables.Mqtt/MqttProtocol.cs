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
        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
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
        var gate = Filters.GetOrAdd(key, static _ => new FilterGate());
        var joinedBroker = false;

        client.ApplicationMessageReceivedAsync += Handler;
        try
        {
            Task subscribeTask;
            lock (gate.Sync)
            {
                gate.Count++;
                if (gate.Count == 1)
                {
                    gate.SubscribeTask = SubscribeBrokerAsync(client, topicFilter, cancellationToken);
                }

                subscribeTask = gate.SubscribeTask;
            }

            await subscribeTask.ConfigureAwait(false);
            joinedBroker = true;

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            client.ApplicationMessageReceivedAsync -= Handler;
            var shouldUnsubscribe = false;
            lock (gate.Sync)
            {
                gate.Count--;
                if (gate.Count <= 0)
                {
                    shouldUnsubscribe = true;
                    Filters.TryRemove(key, out _);
                }
            }

            if (shouldUnsubscribe && joinedBroker)
            {
                try
                {
                    using var unsubCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await client.UnsubscribeAsync(topicFilter, unsubCts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // best-effort if the client is already down
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
        public Task SubscribeTask = Task.CompletedTask;
    }
}
