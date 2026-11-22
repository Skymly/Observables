using System.Runtime.CompilerServices;
using MQTTnet;
using MQTTnet.Protocol;
#if MQTTNET4
using MQTTnet.Client;

namespace Observables.Mqtt.MQTTnet4;
#else
using System.Buffers;
using MQTTnet.Formatter;

namespace Observables.Mqtt.MQTTnet5;
#endif

/// <summary>Adapts a caller-owned MQTTnet client for Observables proxies.</summary>
public static class MqttNetTransport
{
    static readonly ConditionalWeakTable<IMqttClient, Adapter> Adapters = new();

    /// <summary>Returns the transport for this client without managing its connection or lifetime.</summary>
    public static IMqttTransport Wrap(IMqttClient client)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        return Adapters.GetValue(client, static value => new Adapter(value));
    }

    sealed class Adapter(IMqttClient client) : IMqttTransport
    {
        readonly object handlerLock = new();
        Func<string, ReadOnlyMemory<byte>, Task>? handlers;

        public MqttTransportCapabilities Capabilities =>
#if MQTTNET4
            MqttTransportCapabilities.None;
#else
            client.IsConnected && client.Options.ProtocolVersion == MqttProtocolVersion.V500
                ? MqttTransportCapabilities.RequestResponse
                : MqttTransportCapabilities.None;
#endif

        public event Func<string, ReadOnlyMemory<byte>, Task>? MessageReceived
        {
            add
            {
                if (value is null)
                {
                    return;
                }

                lock (handlerLock)
                {
                    if (handlers is null)
                    {
                        client.ApplicationMessageReceivedAsync += OnMessageAsync;
                    }

                    handlers += value;
                }
            }
            remove
            {
                lock (handlerLock)
                {
                    if (handlers is null)
                    {
                        return;
                    }

                    handlers -= value;
                    if (handlers is null)
                    {
                        client.ApplicationMessageReceivedAsync -= OnMessageAsync;
                    }
                }
            }
        }

        async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            Func<string, ReadOnlyMemory<byte>, Task>? snapshot;
            lock (handlerLock)
            {
                snapshot = handlers;
            }

            if (snapshot is null)
            {
                return;
            }

            var message = args.ApplicationMessage;
#if MQTTNET4
            ReadOnlyMemory<byte> payload = message.PayloadSegment;
#else
            ReadOnlyMemory<byte> payload = message.Payload.IsSingleSegment ? message.Payload.First : message.Payload.ToArray();
#endif
            foreach (Func<string, ReadOnlyMemory<byte>, Task> handler in snapshot.GetInvocationList())
            {
                await handler(message.Topic, payload).ConfigureAwait(false);
            }
        }

        public async Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        {
            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(new MqttTopicFilterBuilder().WithTopic(topicFilter).Build())
                .Build();
            var result = await client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);
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

        public async Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        {
            var options = new MqttClientUnsubscribeOptionsBuilder().WithTopicFilter(topicFilter).Build();
            var result = await client.UnsubscribeAsync(options, cancellationToken).ConfigureAwait(false);
            foreach (var item in result.Items)
            {
                if (item.ResultCode is not (
                    MqttClientUnsubscribeResultCode.Success or
                    MqttClientUnsubscribeResultCode.NoSubscriptionExisted))
                {
                    throw new InvalidOperationException($"MQTT unsubscribe rejected: {item.ResultCode}");
                }
            }
        }

        public async Task PublishAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload.ToArray())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            var result = await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"MQTT publish rejected: {result.ReasonCode}");
            }
        }
    }
}
