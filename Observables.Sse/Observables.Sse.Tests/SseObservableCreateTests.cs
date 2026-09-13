using System.Net.Http;
using Observables.Sse.Tests.Infrastructure;
using R3;

namespace Observables.Sse.Tests;

public sealed class SseObservableCreateTests
{
    static readonly Uri Endpoint = new("http://sse.test/stream");

    [Fact]
    public async Task FromEvent_start_failure_completes_with_failure()
    {
        using var http = new HttpClient(new ThrowingSseHandler());
        var observer = new RecordingObserver<string>();

        using var subscription = SseObservable.FromEvent<string>(new SseConnection(http, Endpoint), "price")
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("sse-start-failed", result.Exception.Message);
    }

    [Fact]
    public async Task FromEvent_dispose_disposes_the_response_stream()
    {
        var handler = new HoldOpenSseHandler();
        using var http = new HttpClient(handler);
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SseObservable.FromEvent<string>(new SseConnection(http, Endpoint), "price")
            .Subscribe(value => ready.TrySetResult(value));

        Assert.Equal("ready", await ready.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));

        subscription.Dispose();

        await handler.StreamDisposed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
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
