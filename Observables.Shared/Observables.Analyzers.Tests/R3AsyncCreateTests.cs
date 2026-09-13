using R3;

namespace Observables.Analyzers.Tests;

public sealed class R3AsyncCreateTests
{
    [Fact]
    public async Task R3_Create_drops_exceptions_thrown_before_await()
    {
        var observer = new RecordingObserver<int>();

        using var subscription = Observable.Create<int>(async (o, _) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("create-dropped");
        }).Subscribe(observer);

        await Assert.ThrowsAsync<TimeoutException>(
            () => WaitFor(observer.Completed, TimeSpan.FromMilliseconds(500)));
        Assert.Empty(observer.Items);
        Assert.Empty(observer.Resumes);
    }

    [Fact]
    public async Task Create_completes_with_failure_when_subscribe_throws_before_await()
    {
        var observer = new RecordingObserver<int>();

        using var subscription = R3AsyncCreate.Create<int>(async (o, _) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("wrapper-failure");
        }).Subscribe(observer);

        var result = await WaitFor(observer.Completed, TimeSpan.FromSeconds(2));
        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("wrapper-failure", result.Exception.Message);
        Assert.Empty(observer.Resumes);
    }

    [Fact]
    public async Task Create_completes_successfully_after_subscribe_returns()
    {
        var observer = new RecordingObserver<int>();

        using var subscription = R3AsyncCreate.Create<int>(async (o, _) =>
        {
            o.OnNext(7);
            await Task.CompletedTask.ConfigureAwait(false);
        }).Subscribe(observer);

        var result = await WaitFor(observer.Completed, TimeSpan.FromSeconds(2));
        Assert.True(result.IsSuccess);
        Assert.Equal([7], observer.Items);
        Assert.Empty(observer.Resumes);
    }

    [Fact]
    public async Task Create_is_silent_when_subscription_token_cancels()
    {
        var observer = new RecordingObserver<int>();
        var pumpEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = R3AsyncCreate.Create<int>(async (o, ct) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            }
            finally
            {
                pumpEnded.TrySetResult();
            }
        }).Subscribe(observer);

        subscription.Dispose();
        await WaitFor(pumpEnded.Task, TimeSpan.FromSeconds(2));

        Assert.False(observer.Completed.IsCompleted);
        Assert.Empty(observer.Resumes);
        Assert.Empty(observer.Items);
    }

    [Fact]
    public async Task Create_completes_with_failure_when_caller_token_cancels()
    {
        using var caller = new CancellationTokenSource();
        var observer = new RecordingObserver<int>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = R3AsyncCreate.Create<int>(async (o, _) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, caller.Token).ConfigureAwait(false);
        }).Subscribe(observer);

        await WaitFor(started.Task, TimeSpan.FromSeconds(2));
        caller.Cancel();

        var result = await WaitFor(observer.Completed, TimeSpan.FromSeconds(2));
        Assert.True(result.IsFailure);
        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
        Assert.Empty(observer.Resumes);
    }

    static Task WaitFor(Task task, TimeSpan timeout) =>
        task.WaitAsync(timeout, TestContext.Current.CancellationToken);

    static Task<T> WaitFor<T>(Task<T> task, TimeSpan timeout) =>
        task.WaitAsync(timeout, TestContext.Current.CancellationToken);

    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly List<T> _items = [];
        readonly List<Exception> _resumes = [];
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<T> Items
        {
            get
            {
                lock (_items)
                {
                    return _items.ToArray();
                }
            }
        }

        public IReadOnlyList<Exception> Resumes
        {
            get
            {
                lock (_resumes)
                {
                    return _resumes.ToArray();
                }
            }
        }

        public Task<Result> Completed => _completed.Task;

        protected override void OnNextCore(T value)
        {
            lock (_items)
            {
                _items.Add(value);
            }
        }

        protected override void OnErrorResumeCore(Exception error)
        {
            lock (_resumes)
            {
                _resumes.Add(error);
            }
        }

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }
}
