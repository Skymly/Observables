using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
namespace Observables.Mqtt;

internal static class MqttProtocol
{
    static readonly ConditionalWeakTable<IMqttTransport, ConcurrentDictionary<string, FilterGate>> Filters = new();

    internal static Task PublishAsync(
        IMqttTransport client,
        string topic,
        byte[] payload,
        CancellationToken cancellationToken) =>
        client.PublishAsync(topic, payload ?? Array.Empty<byte>(), cancellationToken);

    internal static async Task SubscribeAsync<T>(
        IMqttTransport client,
        string topicFilter,
        Action<T> onNext,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        if (topicFilter.StartsWith("$share/", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Shared MQTT subscription filters starting with '$share/' are not supported.");
        }

        Task Handler(string topic, ReadOnlyMemory<byte> payload)
        {
            if (cancellationToken.IsCancellationRequested || !MqttTopicMatcher.Matches(topicFilter, topic))
            {
                return Task.CompletedTask;
            }

            try
            {
                onNext(MqttPayloadSerializers.Deserialize<T>(payload.ToArray()));
            }
            catch (Exception ex)
            {
                onError(ex);
            }

            return Task.CompletedTask;
        }

        var filters = Filters.GetValue(client, static _ => new ConcurrentDictionary<string, FilterGate>(StringComparer.Ordinal));
        client.MessageReceived += Handler;
        try
        {
            await AddSubscriberAsync(filters, client, topicFilter).ConfigureAwait(false);
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            client.MessageReceived -= Handler;
            await RemoveSubscriberAsync(filters, client, topicFilter).ConfigureAwait(false);
        }
    }

    static Task AddSubscriberAsync(
        ConcurrentDictionary<string, FilterGate> filters,
        IMqttTransport client,
        string topicFilter)
    {
        while (true)
        {
            var gate = filters.GetOrAdd(topicFilter, static _ => new FilterGate());
            lock (gate.Sync)
            {
                if (gate.Removed)
                {
                    continue;
                }

                gate.Count++;
                if (gate.Count == 1)
                {
                    gate.BrokerWork = ContinueSubscribe(gate.BrokerWork, client, topicFilter);
                }

                return gate.BrokerWork;
            }
        }
    }

    static Task RemoveSubscriberAsync(
        ConcurrentDictionary<string, FilterGate> filters,
        IMqttTransport client,
        string topicFilter)
    {
        if (!filters.TryGetValue(topicFilter, out var gate))
        {
            return Task.CompletedTask;
        }

        lock (gate.Sync)
        {
            gate.Count--;
            if (gate.Count > 0)
            {
                return Task.CompletedTask;
            }

            gate.Count = 0;
            gate.BrokerWork = ContinueUnsubscribe(gate.BrokerWork, client, topicFilter, filters, gate);
            return gate.BrokerWork;
        }
    }

    static async Task ContinueSubscribe(Task previous, IMqttTransport client, string topicFilter)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A failed previous generation must not block a new subscribe.
        }

        await client.SubscribeAsync(topicFilter, CancellationToken.None).ConfigureAwait(false);
    }

    static async Task ContinueUnsubscribe(
        Task previous,
        IMqttTransport client,
        string topicFilter,
        ConcurrentDictionary<string, FilterGate> filters,
        FilterGate gate)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // best-effort if subscribe never completed
        }

        lock (gate.Sync)
        {
            if (gate.Count > 0)
            {
                return;
            }
        }

        try
        {
            using var unsubCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.UnsubscribeAsync(topicFilter, unsubCts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // best-effort if the client is already down
        }
        finally
        {
            lock (gate.Sync)
            {
                if (gate.Count == 0)
                {
                    gate.Removed = true;
                    filters.TryRemove(topicFilter, out _);
                }
            }
        }
    }

    sealed class FilterGate
    {
        public readonly object Sync = new();
        public int Count;
        public bool Removed;
        public Task BrokerWork = Task.CompletedTask;
    }
}
