using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt.Reactive;

/// <summary>Bridges MQTT client APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveMqttAdapter
{

    public static IObservable<System.Reactive.Unit> FromPublish(
        IMqttClient client,
        string topic,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Array.Empty<byte>())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await client.PublishAsync(message, linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        Observable.Create<T>(observer =>
        {
            var gate = new object();
            Func<MqttApplicationMessageReceivedEventArgs, Task>? handler = null;

            _ = SubscribeAsync();

            return Disposable.Create(() =>
            {
                lock (gate)
                {
                    if (handler is not null)
                    {
                        client.ApplicationMessageReceivedAsync -= handler;
                        handler = null;
                    }
                }
            });

            async Task SubscribeAsync()
            {
                try
                {
                    handler = async e =>
                    {
                        if (!TopicMatches(topicFilter, e.ApplicationMessage.Topic))
                        {
                            return;
                        }

                        try
                        {
                            var payload = e.ApplicationMessage.PayloadSegment.Count == 0
                                ? Array.Empty<byte>()
                                : e.ApplicationMessage.PayloadSegment.ToArray();
                            observer.OnNext(MqttPayloadSerializers.Deserialize<T>(payload));
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    };

                    lock (gate)
                    {
                        client.ApplicationMessageReceivedAsync += handler;
                    }

                    await client
                        .SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topicFilter).Build())
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }
        });

    static bool TopicMatches(string filter, string? topic)
    {
        if (topic is null)
        {
            return false;
        }

        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');
        for (var i = 0; i < filterParts.Length; i++)
        {
            if (i >= topicParts.Length)
            {
                return false;
            }

            var fp = filterParts[i];
            if (fp == "#")
            {
                return true;
            }

            if (fp != "+" && fp != topicParts[i])
            {
                return false;
            }
        }

        return filterParts.Length == topicParts.Length;
    }
}
