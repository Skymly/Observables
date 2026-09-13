using Observables.Redis.Tests.Infrastructure;
using R3;

namespace Observables.Redis.Tests;

public sealed class RedisObservableCreateTests
{
    [Fact]
    public async Task FromSubscribe_subscribe_failure_completes_with_failure()
    {
        var multiplexer = HangingRedis.CreateThrowingSubscribe();
        var observer = new RecordingObserver<string>();

        using var subscription = RedisObservable.FromSubscribe<string>(multiplexer, "orders").Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("subscribe-failed", result.Exception.Message);
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
