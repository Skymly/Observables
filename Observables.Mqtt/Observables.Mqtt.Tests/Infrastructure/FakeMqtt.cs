using System.Reflection;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;

namespace Observables.Mqtt.Tests.Infrastructure;

internal static class FakeMqtt
{
    public static FakeMqttClientProxy Create(bool rejectSubscribe = false)
    {
        var client = DispatchProxy.Create<IMqttClient, FakeMqttClientProxy>();
        var proxy = (FakeMqttClientProxy)(object)client;
        proxy.RejectSubscribe = rejectSubscribe;
        return proxy;
    }

    public static IMqttClient AsClient(this FakeMqttClientProxy proxy) => (IMqttClient)(object)proxy;
}

public class FakeMqttClientProxy : DispatchProxy
{
    Func<MqttApplicationMessageReceivedEventArgs, Task>? _received;

    public bool RejectSubscribe { get; set; }

    public int SubscribeCalls;

    public int UnsubscribeCalls;

    public int HandlerCount;

    public bool BrokerUnsubscribed { get; private set; }

    public async Task RaiseAsync(string topic, string payload)
    {
        if (BrokerUnsubscribed)
        {
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(System.Text.Encoding.UTF8.GetBytes(payload))
            .Build();
        var args = new MqttApplicationMessageReceivedEventArgs(
            "client",
            message,
            new MqttPublishPacket(),
            static (_, _) => Task.CompletedTask);
        var handler = _received;
        if (handler is not null)
        {
            await handler(args).ConfigureAwait(false);
        }
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod?.Name)
        {
            case "add_ApplicationMessageReceivedAsync":
                _received += (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                Interlocked.Increment(ref HandlerCount);
                return null;
            case "remove_ApplicationMessageReceivedAsync":
                _received -= (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                Interlocked.Decrement(ref HandlerCount);
                return null;
            case nameof(IMqttClient.SubscribeAsync):
                Interlocked.Increment(ref SubscribeCalls);
                var filter = TopicFilter(args);
                var code = RejectSubscribe
                    ? MqttClientSubscribeResultCode.UnspecifiedError
                    : MqttClientSubscribeResultCode.GrantedQoS0;
                var item = new MqttClientSubscribeResultItem(filter, code);
                return Task.FromResult(
                    new MqttClientSubscribeResult(0, [item], string.Empty, Array.Empty<MqttUserProperty>()));
            case nameof(IMqttClient.UnsubscribeAsync):
                Interlocked.Increment(ref UnsubscribeCalls);
                BrokerUnsubscribed = true;
                return Task.CompletedTask;
            case nameof(IDisposable.Dispose):
                return null;
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }

    static MqttTopicFilter TopicFilter(object?[]? args)
    {
        if (args is { Length: > 0 } && args[0] is MqttClientSubscribeOptions options && options.TopicFilters.Count > 0)
        {
            return options.TopicFilters[0];
        }

        return new MqttTopicFilterBuilder().WithTopic("orders").Build();
    }
}
