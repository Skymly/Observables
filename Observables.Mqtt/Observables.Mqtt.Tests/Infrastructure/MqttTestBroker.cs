using System.Net;
using System.Net.Sockets;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

namespace Observables.Mqtt.Tests.Infrastructure;

/// <summary>In-process MQTT broker for E2E tests.</summary>
public sealed class MqttTestBroker : IAsyncDisposable
{
    readonly MqttServer server;
    readonly MqttFactory factory;
    readonly object gate = new();
    readonly HashSet<(string ClientId, string Topic)> activeSubscriptions = new();
    readonly List<SubscriptionWaiter> waiters = [];

    MqttTestBroker(MqttServer server, MqttFactory factory, int port)
    {
        this.server = server;
        this.factory = factory;
        Port = port;
        server.ClientSubscribedTopicAsync += OnClientSubscribedAsync;
        server.ClientUnsubscribedTopicAsync += OnClientUnsubscribedAsync;
        server.ClientDisconnectedAsync += OnClientDisconnectedAsync;
    }

    public int Port { get; }

    public static async Task<MqttTestBroker> StartAsync(CancellationToken cancellationToken = default)
    {
        var factory = new MqttFactory();
        var port = ReserveFreeTcpPort();
        var options = factory
            .CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();
        var server = factory.CreateMqttServer(options);

        await server.StartAsync().ConfigureAwait(false);
        return new MqttTestBroker(server, factory, port);
    }

    static int ReserveFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Waits until the broker has accepted a subscription for <paramref name="topicFilter"/> from <paramref name="clientId"/>.</summary>
    public Task WaitForSubscriptionAsync(
        string clientId,
        string topicFilter,
        CancellationToken cancellationToken = default) =>
        WaitForStateAsync(clientId, topicFilter, subscribed: true, cancellationToken);

    /// <summary>Waits until the broker has accepted an unsubscription for <paramref name="topicFilter"/> from <paramref name="clientId"/>.</summary>
    public Task WaitForUnsubscriptionAsync(
        string clientId,
        string topicFilter,
        CancellationToken cancellationToken = default) =>
        WaitForStateAsync(clientId, topicFilter, subscribed: false, cancellationToken);

    async Task WaitForStateAsync(
        string clientId,
        string topicFilter,
        bool subscribed,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource? completed = null;
        lock (gate)
        {
            if (activeSubscriptions.Contains((clientId, topicFilter)) == subscribed)
            {
                return;
            }

            completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add(new SubscriptionWaiter(clientId, topicFilter, subscribed, completed));
        }

        try
        {
            await completed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (gate)
            {
                waiters.RemoveAll(waiter => ReferenceEquals(waiter.Completed, completed));
            }
        }
    }

    Task OnClientSubscribedAsync(ClientSubscribedTopicEventArgs args)
    {
        Complete(args.ClientId, args.TopicFilter.Topic, subscribed: true);
        return Task.CompletedTask;
    }

    Task OnClientUnsubscribedAsync(ClientUnsubscribedTopicEventArgs args)
    {
        Complete(args.ClientId, args.TopicFilter, subscribed: false);
        return Task.CompletedTask;
    }

    Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        lock (gate)
        {
            activeSubscriptions.RemoveWhere(subscription =>
                string.Equals(subscription.ClientId, args.ClientId, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }

    void Complete(string clientId, string topicFilter, bool subscribed)
    {
        List<TaskCompletionSource> completed;
        lock (gate)
        {
            if (subscribed)
            {
                activeSubscriptions.Add((clientId, topicFilter));
            }
            else
            {
                activeSubscriptions.Remove((clientId, topicFilter));
            }

            completed = TakeWaiters(clientId, topicFilter, subscribed);
        }

        foreach (TaskCompletionSource waiter in completed)
        {
            waiter.TrySetResult();
        }
    }

    List<TaskCompletionSource> TakeWaiters(string clientId, string topicFilter, bool subscribed)
    {
        List<TaskCompletionSource> completed = [];
        for (var i = waiters.Count - 1; i >= 0; i--)
        {
            SubscriptionWaiter waiter = waiters[i];
            if (waiter.Subscribed != subscribed
                || !string.Equals(waiter.ClientId, clientId, StringComparison.Ordinal)
                || !string.Equals(waiter.TopicFilter, topicFilter, StringComparison.Ordinal))
            {
                continue;
            }

            completed.Add(waiter.Completed);
            waiters.RemoveAt(i);
        }

        return completed;
    }

    public async Task<MqttClientSession> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var clientId = Guid.NewGuid().ToString("N");
        var client = factory.CreateMqttClient();
        var result = await client
            .ConnectAsync(
                new MqttClientOptionsBuilder()
                    .WithTcpServer("127.0.0.1", Port)
                    .WithClientId(clientId)
                    .Build(),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new InvalidOperationException($"MQTT connect failed: {result.ResultCode}");
        }

        return new MqttClientSession(client, clientId);
    }

    public async ValueTask DisposeAsync()
    {
        server.ClientSubscribedTopicAsync -= OnClientSubscribedAsync;
        server.ClientUnsubscribedTopicAsync -= OnClientUnsubscribedAsync;
        server.ClientDisconnectedAsync -= OnClientDisconnectedAsync;
        await server.StopAsync().ConfigureAwait(false);
        server.Dispose();
    }

    public sealed class MqttClientSession(IMqttClient client, string clientId) : IAsyncDisposable
    {
        public IMqttClient Client { get; } = client;

        public string ClientId { get; } = clientId;

        public async ValueTask DisposeAsync()
        {
            if (Client.IsConnected)
            {
                await Client.DisconnectAsync().ConfigureAwait(false);
            }

            if (Client is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    sealed class SubscriptionWaiter(
        string clientId,
        string topicFilter,
        bool subscribed,
        TaskCompletionSource completed)
    {
        public string ClientId { get; } = clientId;

        public string TopicFilter { get; } = topicFilter;

        public bool Subscribed { get; } = subscribed;

        public TaskCompletionSource Completed { get; } = completed;
    }
}
