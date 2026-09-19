using MQTTnet.Client;
using Observables.Mqtt;
using Observables.Mqtt.Reactive;
using Observables.Mqtt.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Mqtt.Reactive.Tests;

public sealed class MqttAdapterCreateTests
{
    [Fact]
    public async Task Fake_UnsubscribeAsync_returns_unsubscribe_result()
    {
        var proxy = FakeMqtt.Create();
        var result = await proxy.AsClient().UnsubscribeAsync("orders", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(1, Volatile.Read(ref proxy.UnsubscribeCalls));
    }

    [Fact]
    public async Task FromSubscribe_dispose_swallows_unsubscribe_failure_without_cast()
    {
        var proxy = FakeMqtt.Create();
        proxy.ThrowOnUnsubscribe = true;
        var subscription = SystemReactiveMqttAdapter.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
        await WaitUntil(() => Volatile.Read(ref proxy.SubscribeCalls) >= 1, "SubscribeCalls");
        subscription.Dispose();
        await WaitUntil(() => Volatile.Read(ref proxy.UnsubscribeCalls) >= 1, "UnsubscribeCalls");
        Assert.Equal(1, Volatile.Read(ref proxy.UnsubscribeCalls));
    }

    [Fact]
    public async Task FromSubscribe_resubscribe_does_not_let_old_unsubscribe_drop_new_generation()
    {
        var proxy = FakeMqtt.Create();
        proxy.UnsubscribeDelayMilliseconds = 200;
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = SystemReactiveMqttAdapter.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
        await WaitUntil(() => Volatile.Read(ref proxy.SubscribeCalls) >= 1, "first subscribe");
        first.Dispose();
        await WaitUntil(() => Volatile.Read(ref proxy.UnsubscribeCalls) >= 1, "unsubscribe started");

        using var second = SystemReactiveMqttAdapter.FromSubscribe<string>(proxy.AsClient(), "orders")
            .Subscribe(value => received.TrySetResult(value));
        await Task.Delay(proxy.UnsubscribeDelayMilliseconds + 150, TestContext.Current.CancellationToken);
        Assert.True(proxy.HasFilter("orders"));

        await proxy.RaiseAsync("orders", "hello");
        Assert.Equal("hello", await received.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.True(proxy.HasFilter("orders"));
    }

    [Fact]
    public async Task FromPublish_not_authorized_fails()
    {
        var proxy = FakeMqtt.Create();
        proxy.PublishReasonCode = MqttClientPublishReasonCode.NotAuthorized;
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveMqttAdapter.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken)
            .Subscribe(_ => { }, ex => error.TrySetResult(ex));

        var ex = await error.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("NotAuthorized", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromPublish_no_matching_subscribers_succeeds()
    {
        var proxy = FakeMqtt.Create();
        proxy.PublishReasonCode = MqttClientPublishReasonCode.NoMatchingSubscribers;

        await SystemReactiveMqttAdapter.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken)
            .Timeout(TimeSpan.FromSeconds(2))
            .FirstAsync()
            .ToTask();
    }

    [Fact]
    public async Task FromPublish_success_completes_with_unit()
    {
        var proxy = FakeMqtt.Create();
        await SystemReactiveMqttAdapter.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken)
            .Timeout(TimeSpan.FromSeconds(2))
            .FirstAsync()
            .ToTask();
    }

    [Fact]
    public async Task FromSubscribe_dollar_wildcard_does_not_receive_sys_when_exact_subscription_delivers()
    {
        var proxy = FakeMqtt.Create();
        var exact = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wildcard = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var exactSub = SystemReactiveMqttAdapter.FromSubscribe<string>(proxy.AsClient(), "$SYS/uptime")
            .Subscribe(value => exact.TrySetResult(value));
        using var wildSub = SystemReactiveMqttAdapter.FromSubscribe<string>(proxy.AsClient(), "#")
            .Subscribe(value => wildcard.TrySetResult(value));

        await WaitUntil(() => Volatile.Read(ref proxy.HandlerCount) >= 2, "Handlers");
        await proxy.RaiseAsync("$SYS/uptime", "1");

        Assert.Equal("1", await exact.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.False(wildcard.Task.IsCompleted);
    }

    [Fact]
    public async Task FromSubscribe_bad_payload_is_terminal_on_rx()
    {
        var proxy = FakeMqtt.Create();
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var values = new List<int>();

        using var subscription = SystemReactiveMqttAdapter.FromSubscribe<int>(proxy.AsClient(), "orders")
            .Subscribe(value => values.Add(value), ex => error.TrySetResult(ex));

        await WaitUntil(() => proxy.HasFilter("orders"), "subscribed");
        await proxy.RaiseAsync("orders", "not-json");
        await proxy.RaiseAsync("orders", "7");

        var ex = await error.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.NotNull(ex);
        Assert.Empty(values);
    }

    static async Task WaitUntil(Func<bool> condition, string name)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"{name} did not reach the expected state.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
