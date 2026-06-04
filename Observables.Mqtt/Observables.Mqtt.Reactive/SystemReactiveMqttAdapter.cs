using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
#if NETSTANDARD2_0
#else
using System.Text.Json;
#endif

namespace Observables.Mqtt.Reactive;

/// <summary>Bridges MQTT client APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveMqttAdapter
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

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
                    await client
                        .SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topicFilter).Build())
                        .ConfigureAwait(false);

                    handler = async e =>
                    {
                        if (!TopicMatches(topicFilter, e.ApplicationMessage.Topic))
                        {
                            return;
                        }

                        try
                        {
                            observer.OnNext(DeserializePayload<T>(e.ApplicationMessage.PayloadSegment.ToArray()));
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
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            }
        });

    static T DeserializePayload<T>(byte[] payload)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)payload;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)Encoding.UTF8.GetString(payload);
        }

#if NETSTANDARD2_0
        throw new NotSupportedException(
            "Deserializing MQTT payloads to types other than byte[] or string requires net8.0 or later.");
#else
        var json = Encoding.UTF8.GetString(payload);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException("MQTT payload deserialized to null.");
        }

        return value;
#endif
    }

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
