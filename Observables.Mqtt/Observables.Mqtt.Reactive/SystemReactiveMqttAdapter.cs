using System.Reactive.Linq;
using Observables.Mqtt;
namespace Observables.Mqtt.Reactive;

/// <summary>Bridges MQTT client APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveMqttAdapter
{
    /// <summary>
    /// Publishes an empty payload to <paramref name="topic"/> at QoS 1.
    /// A rejected PUBACK (any reason other than Success or NoMatchingSubscribers) fails the observable.
    /// Generated v1 publish members use this empty-payload command contract.
    /// </summary>
    public static IObservable<System.Reactive.Unit> FromPublish(
        IMqttTransport client,
        string topic,
        CancellationToken cancellationToken = default) =>
        FromPublish(client, topic, Array.Empty<byte>(), cancellationToken);

    /// <summary>
    /// Publishes <paramref name="payload"/> to <paramref name="topic"/> at QoS 1.
    /// A rejected PUBACK (any reason other than Success or NoMatchingSubscribers) fails the observable.
    /// </summary>
    public static IObservable<System.Reactive.Unit> FromPublish(
        IMqttTransport client,
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await MqttProtocol.PublishAsync(client, topic, payload, linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    /// <summary>
    /// Subscribes to <paramref name="topicFilter"/>. Payload deserialization errors terminate the
    /// subscription through <c>OnError</c>; values are not emitted after that terminal error.
    /// This library does not re-issue <c>SubscribeAsync</c> after the client reconnects.
    /// </summary>
    public static IObservable<T> FromSubscribe<T>(IMqttTransport client, string topicFilter) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            await MqttProtocol
                .SubscribeAsync<T>(client, topicFilter, observer.OnNext, observer.OnError, ct)
                .ConfigureAwait(false);
        });
}
