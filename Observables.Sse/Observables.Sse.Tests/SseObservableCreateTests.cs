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
    public async Task FromEvent_header_timeout_completes_with_failure()
    {
        var handler = new DelayedHeadersSseHandler(TimeSpan.FromSeconds(30));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var observer = new RecordingObserver<string>();

        using var subscription = SseObservable.FromEvent<string>(new SseConnection(http, Endpoint), "price")
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.True(result.Exception is OperationCanceledException or TimeoutException or HttpRequestException);
    }

    [Fact]
    public async Task FromEvent_slow_body_after_headers_still_reads()
    {
        var handler = new SlowBodySseHandler(TimeSpan.FromMilliseconds(400));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(150) };
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SseObservable.FromEvent<string>(new SseConnection(http, Endpoint), "price")
            .Subscribe(value => ready.TrySetResult(value));

        Assert.Equal("late", await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
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

    [Fact]
    public async Task FromEvent_non_positive_max_line_bytes_fails()
    {
        using var http = new HttpClient(new ThrowingSseHandler());
        var observer = new RecordingObserver<string>();
        var connection = new SseConnection(http, Endpoint, maxLineBytes: 0, maxEventBytes: SseConnection.DefaultMaxEventBytes);

        using var subscription = SseObservable.FromEvent<string>(connection, "price").Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.IsType<ArgumentOutOfRangeException>(result.Exception);
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
