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

    [Theory]
    [InlineData("lf")]
    [InlineData("cr")]
    [InlineData("crlf")]
    public async Task Receives_named_string_events_with_standard_line_endings(string lineEnding)
    {
        using var http = new HttpClient();
        var feed = SseService.For<IE2EFeed>(new SseConnection(http, fixture.Server.GetStreamUri(lineEnding)));
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var values = await feed.Prices.Take(2).ToArrayAsync(cts.Token);

        Assert.Equal(["100", "200"], values);
    }

    [Fact]
    public async Task Inherited_properties_with_the_same_name_keep_their_own_event()
    {
        using var http = new HttpClient();
        var feed = SseService.For<ICombinedFeed>(new SseConnection(http, fixture.Server.Uri));
        using var timeout = new CancellationTokenSource(DefaultTimeout);

        var price = ((IPriceFeed)feed).Data.FirstAsync(timeout.Token);
        var heartbeat = ((IHeartbeatFeed)feed).Data.FirstAsync(timeout.Token);

        Assert.Equal("100", await price);
        Assert.Equal("beat", await heartbeat);
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
