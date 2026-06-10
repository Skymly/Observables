using System.Net.Http;
using Observables.Sse;
using Observables.Sse.Tests.Contracts;
using Observables.Sse.Tests.Infrastructure;
using R3;

namespace Observables.Sse.Tests;

[Collection(nameof(SseTestServerCollection))]
public sealed class SseClientR3E2ETests(SseTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    IE2EFeed CreateFeed() =>
        SseService.For<IE2EFeed>(new SseConnection(new HttpClient(), fixture.Server.Uri));

    [Fact]
    public async Task Receives_named_string_event()
    {
        var feed = CreateFeed();
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var first = await feed.Prices.FirstAsync(cts.Token);

        Assert.Equal("100", first);
    }

    [Fact]
    public async Task Receives_default_message_event()
    {
        var feed = CreateFeed();
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var beat = await feed.Heartbeats.FirstAsync(cts.Token);

        Assert.Equal("beat", beat);
    }

    [Fact]
    public async Task Deserializes_json_event_payload()
    {
        var feed = CreateFeed();
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var tick = await feed.Ticks.FirstAsync(cts.Token);

        Assert.Equal(42, tick.Value);
    }
}
