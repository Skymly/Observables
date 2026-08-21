using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using R3;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

/// <summary>Bridges MQTT client APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class MqttObservable
{

    public static Observable<Unit> FromPublish(
        IMqttClient client,
        string topic,
        CancellationToken cancellationToken = default) =>
        FromPublish(client, topic, Array.Empty<byte>(), cancellationToken);

    public static Observable<Unit> FromPublish(
        IMqttClient client,
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload ?? Array.Empty<byte>())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await client.PublishAsync(message, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            async Task Handler(MqttApplicationMessageReceivedEventArgs e)
            {
                if (!MqttTopicMatcher.Matches(topicFilter, e.ApplicationMessage.Topic))
                {
                    return;
                }

                try
                {
                    var payload = e.ApplicationMessage.PayloadSegment.Count == 0
                        ? Array.Empty<byte>()
                        : e.ApplicationMessage.PayloadSegment.ToArray();
                    observer.OnNext(MqttPayload.Deserialize<T>(payload));
                }
                catch (Exception ex)
                {
                    observer.OnErrorResume(ex);
                }

                await Task.CompletedTask.ConfigureAwait(false);
            }

            client.ApplicationMessageReceivedAsync += Handler;
            try
            {
                await client
                    .SubscribeAsync(
                        new MqttTopicFilterBuilder().WithTopic(topicFilter).Build(),
                        ct)
                    .ConfigureAwait(false);

                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
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
        });
}
