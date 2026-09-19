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
    readonly object _filterLock = new();
    readonly HashSet<string> _filters = new(StringComparer.Ordinal);
    Func<MqttApplicationMessageReceivedEventArgs, Task>? _received;

    public bool RejectSubscribe { get; set; }

    public bool ThrowOnUnsubscribe { get; set; }

    public int SubscribeDelayMilliseconds { get; set; }

    public int UnsubscribeDelayMilliseconds { get; set; }

    public MqttClientPublishReasonCode PublishReasonCode { get; set; } = MqttClientPublishReasonCode.Success;

    public int SubscribeCalls;

    public int UnsubscribeCalls;

    public int HandlerCount;

    public int FilterCount
    {
        get
        {
            lock (_filterLock)
            {
                return _filters.Count;
            }
        }
    }

    public bool HasFilter(string topicFilter)
    {
        lock (_filterLock)
        {
            return _filters.Contains(topicFilter);
        }
    }

    public async Task RaiseAsync(string topic, string payload)
    {
        lock (_filterLock)
        {
            if (_filters.Count == 0)
            {
                return;
            }
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
                var result = new MqttClientSubscribeResult(0, [item], string.Empty, Array.Empty<MqttUserProperty>());
                var cancellation = Cancellation(args);
                if (SubscribeDelayMilliseconds <= 0)
                {
                    if (!RejectSubscribe)
                    {
                        AddFilter(filter.Topic);
                    }

                    return Task.FromResult(result);
                }

                return DelaySubscribeAsync(result, filter.Topic, cancellation);
            case nameof(IMqttClient.UnsubscribeAsync):
                Interlocked.Increment(ref UnsubscribeCalls);
                if (ThrowOnUnsubscribe)
                {
                    return Task.FromException<MqttClientUnsubscribeResult>(
                        new InvalidOperationException("unsubscribe failed"));
                }

                var topic = UnsubscribeTopic(args);
                var unsubResult = new MqttClientUnsubscribeResult(
                    0,
                    Array.Empty<MqttClientUnsubscribeResultItem>(),
                    string.Empty,
                    Array.Empty<MqttUserProperty>());
                if (UnsubscribeDelayMilliseconds <= 0)
                {
                    RemoveFilter(topic);
                    return Task.FromResult(unsubResult);
                }

                return DelayUnsubscribeAsync(unsubResult, topic, Cancellation(args));
            case nameof(IMqttClient.PublishAsync):
                var publishResult = new MqttClientPublishResult(
                    0,
                    PublishReasonCode,
                    string.Empty,
                    Array.Empty<MqttUserProperty>());
                return Task.FromResult(publishResult);
            case nameof(IDisposable.Dispose):
                return null;
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }

    async Task<MqttClientSubscribeResult> DelaySubscribeAsync(
        MqttClientSubscribeResult result,
        string topic,
        CancellationToken cancellationToken)
    {
        await Task.Delay(SubscribeDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        if (!RejectSubscribe)
        {
            AddFilter(topic);
        }

        return result;
    }

    async Task<MqttClientUnsubscribeResult> DelayUnsubscribeAsync(
        MqttClientUnsubscribeResult result,
        string? topic,
        CancellationToken cancellationToken)
    {
        await Task.Delay(UnsubscribeDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        RemoveFilter(topic);
        return result;
    }

    void AddFilter(string topic)
    {
        lock (_filterLock)
        {
            _filters.Add(topic);
        }
    }

    void RemoveFilter(string? topic)
    {
        if (topic is null)
        {
            return;
        }

        lock (_filterLock)
        {
            _filters.Remove(topic);
        }
    }

    static CancellationToken Cancellation(object?[]? args)
    {
        if (args is { Length: > 1 } && args[1] is CancellationToken cancellationToken)
        {
            return cancellationToken;
        }

        return default;
    }

    static MqttTopicFilter TopicFilter(object?[]? args)
    {
        if (args is { Length: > 0 } && args[0] is MqttClientSubscribeOptions options && options.TopicFilters.Count > 0)
        {
            return options.TopicFilters[0];
        }

        return new MqttTopicFilterBuilder().WithTopic("orders").Build();
    }

    static string? UnsubscribeTopic(object?[]? args)
    {
        if (args is { Length: > 0 } && args[0] is MqttClientUnsubscribeOptions options && options.TopicFilters.Count > 0)
        {
            return options.TopicFilters[0];
        }

        return null;
    }
}
