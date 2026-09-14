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
