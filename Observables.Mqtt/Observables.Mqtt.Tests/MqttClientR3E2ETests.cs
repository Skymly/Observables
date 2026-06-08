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
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        var hub = MqttService.For<IE2EHub>(session.Client);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            session.ClientId,
            "e2e/ping",
            cts.Token);
        var receive = hub.Ping.FirstAsync(cts.Token);
        await waitSubscription;
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
        await using var subscriber = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        await using var publisher = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        var subHub = MqttService.For<IE2EHub>(subscriber.Client);
        var pubHub = MqttService.For<IE2EHub>(publisher.Client);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            subscriber.ClientId,
            "e2e/ping",
            cts.Token);
        var receive = subHub.Ping.FirstAsync(cts.Token);
        await waitSubscription;
        await pubHub.PublishPing().FirstAsync(cts.Token);

        Assert.Equal(string.Empty, await receive);
    }
}
