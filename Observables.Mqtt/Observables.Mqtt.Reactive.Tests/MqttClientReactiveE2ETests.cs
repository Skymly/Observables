using MQTTnet;
using MQTTnet.Client;
using Observables.Mqtt;
using Observables.Mqtt.Reactive;
using Observables.Mqtt.Reactive.Tests.Contracts;
using Observables.Mqtt.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Mqtt.Reactive.Tests;

[Collection(nameof(MqttTestBrokerCollection))]
public sealed class MqttClientReactiveE2ETests(MqttTestBrokerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MqttSubscribe_Ping_receives_broker_message()
    {
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        var hub = MqttService.For<IE2EHub>(session.Transport);

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
        var subHub = MqttService.For<IE2EHub>(subscriber.Transport);
        var pubHub = MqttService.For<IE2EHub>(publisher.Transport);

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

    [Fact]
    public async Task FromPublish_sends_raw_bytes_to_the_broker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var subscriber = await fixture.Broker.ConnectAsync(cancellationToken);
        await using var publisher = await fixture.Broker.ConnectAsync(cancellationToken);
        const string topic = "/e2e/raw-bytes";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        var subscribed = fixture.Broker.WaitForSubscriptionAsync(subscriber.ClientId, topic, timeout.Token);
        var received = SystemReactiveMqttAdapter.FromSubscribe<byte[]>(subscriber.Transport, topic)
            .FirstAsync().ToTask(timeout.Token);
        await subscribed;

        byte[] payload = [0, 0xFF, 0x7F];
        await SystemReactiveMqttAdapter.FromPublish(publisher.Transport, topic, payload, timeout.Token)
            .FirstAsync().ToTask(timeout.Token);

        Assert.Equal(payload, await received);
    }

    [Fact]
    public async Task FromSubscribe_multi_level_wildcard_receives_parent_topic()
    {
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource(DefaultTimeout);
        const string topicFilter = "e2e/hash/#";

        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            session.ClientId,
            topicFilter,
            cts.Token);
        var receive = SystemReactiveMqttAdapter.FromSubscribe<string>(session.Transport, topicFilter)
            .Timeout(DefaultTimeout)
            .FirstAsync()
            .ToTask();
        await waitSubscription;
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("e2e/hash")
            .WithPayload("zero"u8.ToArray())
            .Build();
        await session.Client.PublishAsync(message, cts.Token);

        Assert.Equal("zero", await receive);
    }

    [Fact]
    public async Task FromSubscribe_dispose_unsubscribes_topic_filter()
    {
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource(DefaultTimeout);
        const string topicFilter = "e2e/unsub";

        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            session.ClientId,
            topicFilter,
            cts.Token);
        var subscription = SystemReactiveMqttAdapter.FromSubscribe<string>(session.Transport, topicFilter)
            .Subscribe(_ => { });
        try
        {
            await waitSubscription;
            using var unsubCts = new CancellationTokenSource(DefaultTimeout);
            var waitUnsubscription = fixture.Broker.WaitForUnsubscriptionAsync(
                session.ClientId,
                topicFilter,
                unsubCts.Token);
            subscription.Dispose();
            await waitUnsubscription;
        }
        finally
        {
            subscription.Dispose();
        }
    }


    [Fact]
    public async Task FromSubscribe_clean_session_reconnect_does_not_restore_broker_subscription()
    {
        await using var session = await fixture.Broker.ConnectAsync(TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var waitSubscription = fixture.Broker.WaitForSubscriptionAsync(
            session.ClientId,
            "e2e/ping",
            cts.Token);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = SystemReactiveMqttAdapter.FromSubscribe<string>(session.Transport, "e2e/ping")
            .Subscribe(value => received.TrySetResult(value));
        await waitSubscription;

        await session.Client.DisconnectAsync(cancellationToken: cts.Token);
        var reconnect = await session.Client.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithTcpServer("127.0.0.1", fixture.Broker.Port)
                .WithClientId(session.ClientId)
                .WithCleanSession()
                .Build(),
            cts.Token);
        Assert.Equal(MqttClientConnectResultCode.Success, reconnect.ResultCode);

        await using var publisher = await fixture.Broker.ConnectAsync(cts.Token);
        await publisher.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic("e2e/ping")
                .WithPayload("hello"u8.ToArray())
                .Build(),
            cts.Token);

        var finished = await Task.WhenAny(received.Task, Task.Delay(500, cts.Token));
        Assert.NotSame(received.Task, finished);
        Assert.False(received.Task.IsCompleted);
    }
}
