using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

internal static class MqttProtocol
{
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

        client.ApplicationMessageReceivedAsync += Handler;
        try
        {
            await client
                .SubscribeAsync(
                    new MqttTopicFilterBuilder().WithTopic(topicFilter).Build(),
                    cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            client.ApplicationMessageReceivedAsync -= Handler;
            try
            {
                await client.UnsubscribeAsync(topicFilter).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // best-effort if the client is already down
            }
        }
    }
}
