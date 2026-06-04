using MQTTnet;
using MQTTnet.Client;
using Observables.Mqtt;
using Observables.Mqtt.Tests.Contracts;
using Observables.Mqtt.Tests.Infrastructure;
using R3;

namespace Observables.Mqtt.Tests;

[Collection(nameof(MqttTestBrokerCollection))]
public sealed class MqttClientR3E2ETests(MqttTestBrokerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task MqttSubscribe_Ping_receives_broker_message()
    {
        await using var session = await fixture.Broker.ConnectAsync();
        var hub = MqttService.For<IE2EHub>(session.Client);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(cts.Token);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("e2e/ping")
            .WithPayload("hello"u8.ToArray())
            .Build();
        await session.Client.PublishAsync(message, cts.Token);

        Assert.Equal("hello", await receive);
    }

    [Fact]
    public async Task MqttPublish_PublishPing_reaches_subscriber()
    {
        await using var session = await fixture.Broker.ConnectAsync();
        var hub = MqttService.For<IE2EHub>(session.Client);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(cts.Token);
        await hub.PublishPing().FirstAsync(cts.Token);

        Assert.Equal(string.Empty, await receive);
    }
}
