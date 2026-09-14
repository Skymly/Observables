using Microsoft.AspNetCore.SignalR.Client;
using R3;
using Observables.SignalR.Tests.Infrastructure;

namespace Observables.SignalR.Tests;

public sealed class SignalRObservableCreateTests
{
    [Fact]
    public async Task FromStream_unstarted_connection_completes_with_failure()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:1/hub")
            .Build();

        var observer = new RecordingObserver<int>();
        using var subscription = SignalRObservable.FromStream<int>(connection, "Counter", TestContext.Current.CancellationToken).Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task FromOn_second_subscriber_does_not_register_another_handler()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:1/hub")
            .Build();

        using var first = SignalRObservable.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        await WaitForHandlerCountAsync(connection, "Notify", expected: 1);

        using var second = SignalRObservable.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(1, HubConnectionOnHandlers.Count(connection, "Notify"));
    }

    [Fact]
    public async Task FromOn_last_unsubscribe_removes_handler()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:1/hub")
            .Build();

        var first = SignalRObservable.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        var second = SignalRObservable.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        await WaitForHandlerCountAsync(connection, "Notify", expected: 1);

        first.Dispose();
        await WaitForHandlerCountAsync(connection, "Notify", expected: 1);

        second.Dispose();
        await WaitForHandlerCountAsync(connection, "Notify", expected: 0);
    }

    static async Task WaitForHandlerCountAsync(HubConnection connection, string methodName, int expected)
    {
        for (var i = 0; i < 50; i++)
        {
            if (HubConnectionOnHandlers.Count(connection, methodName) == expected)
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Handler count stayed at {HubConnectionOnHandlers.Count(connection, methodName)}, expected {expected}.");
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
