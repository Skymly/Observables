using MQTTnet.Client;
using R3;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Mqtt;

/// <summary>Bridges MQTT client APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class MqttObservable
{
    /// <summary>
    /// Publishes an empty payload to <paramref name="topic"/> at QoS 1.
    /// A rejected PUBACK (any reason other than Success or NoMatchingSubscribers) fails the observable.
    /// </summary>
    public static Observable<Unit> FromPublish(
        IMqttClient client,
        string topic,
        CancellationToken cancellationToken = default) =>
        FromPublish(client, topic, Array.Empty<byte>(), cancellationToken);

    /// <summary>
    /// Publishes <paramref name="payload"/> to <paramref name="topic"/> at QoS 1.
    /// A rejected PUBACK (any reason other than Success or NoMatchingSubscribers) fails the observable.
    /// </summary>
    public static Observable<Unit> FromPublish(
        IMqttClient client,
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await MqttProtocol.PublishAsync(client, topic, payload, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

    /// <summary>
    /// Subscribes to <paramref name="topicFilter"/>. Payload deserialization errors are reported with
    /// OnErrorResume and the subscription keeps receiving later messages.
    /// This library does not re-issue <c>SubscribeAsync</c> after the client reconnects.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        R3AsyncCreate.Create<T>(async (observer, ct) =>
        {
            await MqttProtocol
                .SubscribeAsync<T>(client, topicFilter, observer.OnNext, observer.OnErrorResume, ct)
                .ConfigureAwait(false);
        });
}
