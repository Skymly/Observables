using System.Reactive.Linq;
using MQTTnet.Client;
using Observables.Mqtt;
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
            await MqttProtocol.PublishAsync(client, topic, Array.Empty<byte>(), linked.Token).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.")]
    [RequiresDynamicCode("JSON payload serialization uses System.Text.Json reflection.")]
#endif
    public static IObservable<T> FromSubscribe<T>(IMqttClient client, string topicFilter) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            await MqttProtocol
                .SubscribeAsync<T>(client, topicFilter, observer.OnNext, observer.OnError, ct)
                .ConfigureAwait(false);
        });
}
