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
        using var http = new HttpClient();
        var connection = new SseConnection(http, fixture.Server.KeepAliveUri);

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
        await Task.Delay(200, cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }
}
