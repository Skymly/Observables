using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using R3;
#if NETSTANDARD2_0
#else
using System.Text.Json;
#endif

namespace Observables.Mqtt;

/// <summary>Bridges MQTT client APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class MqttObservable
{
#if NETSTANDARD2_0
#else
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
#endif

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

    public static Observable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            await client
                .SubscribeAsync(
                    new MqttTopicFilterBuilder().WithTopic(topicFilter).Build(),
                    ct)
                .ConfigureAwait(false);

            async Task Handler(MqttApplicationMessageReceivedEventArgs e)
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
                    observer.OnNext(DeserializePayload<T>(payload));
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
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            }
            finally
            {
                client.ApplicationMessageReceivedAsync -= Handler;
            }
        });

    internal static T DeserializePayload<T>(byte[] payload)
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
