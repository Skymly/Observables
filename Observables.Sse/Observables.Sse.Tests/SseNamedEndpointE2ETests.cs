using System.Net.Http;
using Observables.Sse.Tests.Contracts;
using Observables.Sse.Tests.Infrastructure;
using R3;

namespace Observables.Sse.Tests;

[Collection(nameof(SseTestServerCollection))]
public sealed class SseNamedEndpointE2ETests(SseTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Name_on_the_attribute_resolves_the_registered_endpoint()
    {
        SseService.RegisterEndpoint("e2e-named", new SseConnection(new HttpClient(), fixture.Server.Uri));

        var feed = SseService.For<IE2ENamedFeed>();
        using var cts = new CancellationTokenSource(DefaultTimeout);

        var first = await feed.Prices.FirstAsync(cts.Token);

        Assert.Equal("100", first);
    }

    [Fact]
    public void Resolving_a_name_with_no_registered_endpoint_says_which_name_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(
            static () => SseService.For(typeof(IE2EUnregisteredFeed)));

        Assert.Contains("e2e-unregistered", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SseService.RegisterEndpoint), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_an_unnamed_interface_points_at_the_connection_overload()
    {
        var error = Assert.Throws<InvalidOperationException>(static () => SseService.For(typeof(IE2EFeed)));

        Assert.Contains("[Sse(", error.Message, StringComparison.Ordinal);
        Assert.Contains("For<T>(connection)", error.Message, StringComparison.Ordinal);
    }
}
