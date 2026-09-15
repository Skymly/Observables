using MQTTnet;
using Observables.Mqtt;
using Observables.Mqtt.Tests.Contracts;
using Observables.Mqtt.Tests.Infrastructure;
using R3;

namespace Observables.Mqtt.Tests;

[Collection(nameof(MqttTestBrokerCollection))]
public sealed class MqttNamedClientE2ETests(MqttTestBrokerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Name_on_the_attribute_resolves_the_registered_client()
    {
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        MqttService.RegisterClient("e2e-named", session.Client);

        var hub = MqttService.For<IE2ENamedHub>();

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            session.ClientId,
            "e2e/named",
            cts.Token);
        var receive = hub.Ping.FirstAsync(cts.Token);
        await waitSubscription;
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("e2e/named")
            .WithPayload("hello"u8.ToArray())
            .Build();
        await session.Client.PublishAsync(message, cts.Token);

        Assert.Equal("hello", await receive);
    }

    [Fact]
    public void Resolving_a_name_with_no_registered_client_says_which_name_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(
            static () => MqttService.For(typeof(IE2EUnregisteredHub)));

        Assert.Contains("e2e-unregistered", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MqttService.RegisterClient), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_an_unnamed_interface_points_at_the_client_overload()
    {
        var error = Assert.Throws<InvalidOperationException>(static () => MqttService.For(typeof(IE2EHub)));

        Assert.Contains("[Mqtt(", error.Message, StringComparison.Ordinal);
        Assert.Contains("For<T>(client)", error.Message, StringComparison.Ordinal);
    }
}
