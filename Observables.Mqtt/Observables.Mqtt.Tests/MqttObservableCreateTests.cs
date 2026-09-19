using MQTTnet.Client;
using Observables.Mqtt.Tests.Infrastructure;
using R3;

namespace Observables.Mqtt.Tests;

public sealed class MqttObservableCreateTests
{
    [Fact]
    public async Task FromSubscribe_rejected_subscribe_completes_with_failure()
    {
        var proxy = FakeMqtt.Create(rejectSubscribe: true);
        var observer = new RecordingObserver<string>();

        using var subscription = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("UnspecifiedError", result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromSubscribe_sibling_still_receives_after_other_subscription_disposes()
    {
        var proxy = FakeMqtt.Create();
        var remaining = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var first = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
        using var second = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders")
            .Subscribe(value => remaining.TrySetResult(value));

        await WaitForHandlers(proxy, 2);
        first.Dispose();

        await proxy.RaiseAsync("orders", "hello");

        Assert.Equal("hello", await remaining.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.Equal(0, Volatile.Read(ref proxy.UnsubscribeCalls));
    }

    [Fact]
    public async Task FromSubscribe_dispose_unsubscribes_when_subscribe_is_still_in_flight()
    {
        var proxy = FakeMqtt.Create();
        proxy.SubscribeDelayMilliseconds = 200;
        var subscription = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
        try
        {
            await WaitUntil(
                () => Volatile.Read(ref proxy.SubscribeCalls) >= 1,
                "SubscribeCalls");
            subscription.Dispose();
            await WaitUntil(
                () => Volatile.Read(ref proxy.UnsubscribeCalls) >= 1,
                "UnsubscribeCalls");
        }
        finally
        {
            subscription.Dispose();
        }

        Assert.Equal(1, Volatile.Read(ref proxy.UnsubscribeCalls));
    }

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
        var subscription = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
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

        var first = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders").Subscribe(_ => { });
        await WaitUntil(() => Volatile.Read(ref proxy.SubscribeCalls) >= 1, "first subscribe");
        first.Dispose();
        await WaitUntil(() => Volatile.Read(ref proxy.UnsubscribeCalls) >= 1, "unsubscribe started");

        using var second = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "orders")
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
        var observer = new RecordingObserver<Unit>();

        using var subscription = MqttObservable.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken).Subscribe(observer);
        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("NotAuthorized", result.Exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromPublish_no_matching_subscribers_succeeds()
    {
        var proxy = FakeMqtt.Create();
        proxy.PublishReasonCode = MqttClientPublishReasonCode.NoMatchingSubscribers;
        var observer = new RecordingObserver<Unit>();

        using var subscription = MqttObservable.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken).Subscribe(observer);
        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FromPublish_success_completes_with_unit()
    {
        var proxy = FakeMqtt.Create();
        var observer = new RecordingObserver<Unit>();

        using var subscription = MqttObservable.FromPublish(proxy.AsClient(), "orders", TestContext.Current.CancellationToken).Subscribe(observer);
        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FromSubscribe_dollar_wildcard_does_not_receive_sys_when_exact_subscription_delivers()
    {
        var proxy = FakeMqtt.Create();
        var exact = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wildcard = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var exactSub = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "$SYS/uptime")
            .Subscribe(value => exact.TrySetResult(value));
        using var wildSub = MqttObservable.FromSubscribe<string>(proxy.AsClient(), "#")
            .Subscribe(value => wildcard.TrySetResult(value));

        await WaitForHandlers(proxy, 2);
        await proxy.RaiseAsync("$SYS/uptime", "1");

        Assert.Equal("1", await exact.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.False(wildcard.Task.IsCompleted);
    }

    [Fact]
    public async Task FromSubscribe_bad_payload_is_recoverable_on_r3()
    {
        var proxy = FakeMqtt.Create();
        var values = new List<int>();
        var errors = new List<Exception>();
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = MqttObservable.FromSubscribe<int>(proxy.AsClient(), "orders")
            .Subscribe(
                value =>
                {
                    values.Add(value);
                    received.TrySetResult(value);
                },
                error => errors.Add(error),
                _ => { });

        await WaitUntil(() => proxy.HasFilter("orders"), "subscribed");
        await proxy.RaiseAsync("orders", "not-json");
        await proxy.RaiseAsync("orders", "7");

        Assert.Equal(7, await received.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Assert.Equal(new[] { 7 }, values);
        Assert.NotEmpty(errors);
    }

    static Task WaitForHandlers(FakeMqttClientProxy proxy, int count) =>
        WaitUntil(() => Volatile.Read(ref proxy.HandlerCount) >= count, "Handlers");

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

    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Result> Completed => _completed.Task;

        protected override void OnNextCore(T value)
        {
        }

        protected override void OnErrorResumeCore(Exception error)
        {
        }

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }
}
