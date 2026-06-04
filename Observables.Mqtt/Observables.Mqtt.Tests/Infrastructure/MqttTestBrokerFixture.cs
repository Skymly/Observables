namespace Observables.Mqtt.Tests.Infrastructure;

public sealed class MqttTestBrokerFixture : IAsyncLifetime, IAsyncDisposable
{
    public MqttTestBroker Broker { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Broker = await MqttTestBroker.StartAsync();

    public async ValueTask DisposeAsync() => await Broker.DisposeAsync().ConfigureAwait(false);
}

[CollectionDefinition(nameof(MqttTestBrokerCollection))]
public sealed class MqttTestBrokerCollection : ICollectionFixture<MqttTestBrokerFixture>;
