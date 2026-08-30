using MQTTnet.Client;
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
            await MqttProtocol.PublishAsync(client, topic, payload, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static Observable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            await MqttProtocol
                .SubscribeAsync<T>(client, topicFilter, observer.OnNext, observer.OnErrorResume, ct)
                .ConfigureAwait(false);
        });
}
