using NATS.Client.Core;
using Observables.Nats.Tests.Contracts;
using Observables.Nats.Tests.Infrastructure;
using R3;

namespace Observables.Nats.Tests;

[Collection(nameof(NatsTestServerCollection))]
public sealed class NatsNamedConnectionE2ETests(NatsTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Name_on_the_attribute_resolves_the_registered_connection()
    {
        await using var subscriber = new NatsConnection(new NatsOpts { Url = fixture.Server.Url });
        await using var publisher = new NatsConnection(new NatsOpts { Url = fixture.Server.Url });
        NatsService.RegisterConnection("e2e-named", subscriber);

        var subHub = NatsService.For<IE2ENamedHub>();
        var pubHub = NatsService.For<IE2ENamedHub>(publisher);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = subHub.Ping.FirstAsync(cts.Token);
        await NatsE2EHelpers.PublishUntilReceivedAsync(
            async ct => await pubHub.PublishPing().FirstAsync(ct),
            receive,
            cts.Token);

        Assert.Equal(string.Empty, await receive);
    }

    [Fact]
    public void Resolving_a_name_with_no_registered_connection_says_which_name_is_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(
            static () => NatsService.For(typeof(IE2EUnregisteredHub)));

        Assert.Contains("e2e-unregistered", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(NatsService.RegisterConnection), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_an_unnamed_interface_points_at_the_connection_overload()
    {
        var error = Assert.Throws<InvalidOperationException>(static () => NatsService.For(typeof(IE2EHub)));

        Assert.Contains("[Nats(", error.Message, StringComparison.Ordinal);
        Assert.Contains("For<T>(connection)", error.Message, StringComparison.Ordinal);
    }
}
