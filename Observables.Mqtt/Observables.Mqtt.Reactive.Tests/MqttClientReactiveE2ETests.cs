using MQTTnet;
using MQTTnet.Client;
using Observables.Mqtt;
using Observables.Mqtt.Reactive.Tests.Contracts;
using Observables.Mqtt.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Mqtt.Reactive.Tests;

[Collection(nameof(MqttTestBrokerCollection))]
public sealed class MqttClientReactiveE2ETests(MqttTestBrokerFixture fixture)
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
        var receive = hub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask();
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
        var receive = subHub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await waitSubscription;
        await pubHub.PublishPing().Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal(string.Empty, await receive);
    }
}
