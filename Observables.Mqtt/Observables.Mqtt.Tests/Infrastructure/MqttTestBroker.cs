using System.Net;
using System.Net.Sockets;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Observables.Mqtt.Tests.Infrastructure;

/// <summary>In-process MQTT broker for E2E tests.</summary>
public sealed class MqttTestBroker : IAsyncDisposable
{
    readonly MqttServer server;
    readonly MqttFactory factory;

    MqttTestBroker(MqttServer server, MqttFactory factory, int port)
    {
        this.server = server;
        this.factory = factory;
        Port = port;
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

    /// <summary>Waits until the broker accepts a subscription for <paramref name="topicFilter"/> from <paramref name="clientId"/>.</summary>
    public async Task WaitForSubscriptionAsync(
        string clientId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(InterceptingSubscriptionEventArgs e)
        {
            if (string.Equals(e.ClientId, clientId, StringComparison.Ordinal)
                && string.Equals(e.TopicFilter.Topic, topicFilter, StringComparison.Ordinal))
            {
                tcs.TrySetResult();
            }

            return Task.CompletedTask;
        }

        server.InterceptingSubscriptionAsync += Handler;
        try
        {
            await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            server.InterceptingSubscriptionAsync -= Handler;
        }
    }

    /// <summary>Waits until the broker accepts an unsubscription for <paramref name="topicFilter"/> from <paramref name="clientId"/>.</summary>
    public async Task WaitForUnsubscriptionAsync(
        string clientId,
        string topicFilter,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(InterceptingUnsubscriptionEventArgs e)
        {
            if (string.Equals(e.ClientId, clientId, StringComparison.Ordinal)
                && string.Equals(e.Topic, topicFilter, StringComparison.Ordinal))
            {
                tcs.TrySetResult();
            }

            return Task.CompletedTask;
        }

        server.InterceptingUnsubscriptionAsync += Handler;
        try
        {
            await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            server.InterceptingUnsubscriptionAsync -= Handler;
        }
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
}
