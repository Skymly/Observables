using MQTTnet;
using MQTTnet.Client;
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
        var port = Random.Shared.Next(50_000, 60_000);
        var options = factory
            .CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();
        var server = factory.CreateMqttServer(options);

        await server.StartAsync().ConfigureAwait(false);
        return new MqttTestBroker(server, factory, port);
    }

    public async Task<MqttClientSession> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var client = factory.CreateMqttClient();
        var result = await client
            .ConnectAsync(
                new MqttClientOptionsBuilder()
                    .WithTcpServer("127.0.0.1", Port)
                    .WithClientId(Guid.NewGuid().ToString("N"))
                    .Build(),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new InvalidOperationException($"MQTT connect failed: {result.ResultCode}");
        }

        return new MqttClientSession(client);
    }

    public async ValueTask DisposeAsync()
    {
        await server.StopAsync().ConfigureAwait(false);
        server.Dispose();
    }

    public sealed class MqttClientSession(IMqttClient client) : IAsyncDisposable
    {
        public IMqttClient Client { get; } = client;

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
