using Microsoft.AspNetCore.SignalR.Client;
using R3;

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
