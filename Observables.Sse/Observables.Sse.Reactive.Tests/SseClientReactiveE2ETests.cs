using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Observables.Sse;
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
}
