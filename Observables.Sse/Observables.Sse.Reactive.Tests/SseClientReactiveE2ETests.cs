using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Observables.Sse;
using Observables.Sse.Reactive;
using Observables.Sse.Reactive.Tests.Contracts;
using Observables.Sse.Tests.Infrastructure;

namespace Observables.Sse.Reactive.Tests;

[Collection(nameof(SseTestServerCollection))]
public sealed class SseClientReactiveE2ETests(SseTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    IE2EFeed CreateFeed() =>
        SseService.For<IE2EFeed>(new SseConnection(new HttpClient(), fixture.Server.Uri));

    [Fact]
    public async Task Receives_named_string_event()
    {
        var feed = CreateFeed();

        var first = await feed.Prices.Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal("100", first);
    }

    [Fact]
    public async Task Receives_default_message_event()
    {
        var feed = CreateFeed();

        var beat = await feed.Heartbeats.Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal("beat", beat);
    }

    [Fact]
    public async Task Deserializes_json_event_payload()
    {
        var feed = CreateFeed();

        var tick = await feed.Ticks.Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal(42, tick.Value);
    }

    [Fact]
    public async Task FromEvent_dispose_cancels_the_pump_without_completing()
    {
        var handler = new HoldOpenSseHandler();
        using var http = new HttpClient(handler);
        var connection = new SseConnection(http, new Uri("http://sse.test/stream"));

        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveSseAdapter.FromEvent<string>(connection, "price")
            .Subscribe(
                value => ready.TrySetResult(value),
                _ => Interlocked.Exchange(ref errored, 1),
                () => Interlocked.Exchange(ref completed, 1));

        using var cts = new CancellationTokenSource(DefaultTimeout);
        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await handler.StreamDisposed.WaitAsync(cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }

    [Fact]
    public async Task FromEvent_header_timeout_errors()
    {
        var handler = new DelayedHeadersSseHandler(TimeSpan.FromSeconds(30));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var connection = new SseConnection(http, new Uri("http://sse.test/stream"));

        var errored = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveSseAdapter.FromEvent<string>(connection, "price")
            .Subscribe(
                _ => { },
                ex => errored.TrySetResult(ex),
                () => completed.TrySetResult());

        var finished = await Task.WhenAny(errored.Task, completed.Task)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Same(errored.Task, finished);
        Assert.False(completed.Task.IsCompleted);
        var error = await errored.Task;
        Assert.True(error is OperationCanceledException or TimeoutException or HttpRequestException);
    }

    [Fact]
    public async Task FromEvent_slow_body_after_headers_still_reads()
    {
        var handler = new SlowBodySseHandler(TimeSpan.FromMilliseconds(400));
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(150) };
        var connection = new SseConnection(http, new Uri("http://sse.test/stream"));
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveSseAdapter.FromEvent<string>(connection, "price")
            .Subscribe(value => ready.TrySetResult(value));

        Assert.Equal("late", await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
