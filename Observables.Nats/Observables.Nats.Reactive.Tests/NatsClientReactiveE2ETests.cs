using NATS.Client.Core;
using Observables.Nats;
using Observables.Nats.Reactive.Tests.Contracts;
using Observables.Nats.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Nats.Reactive.Tests;

[Collection(nameof(NatsTestServerCollection))]
public sealed class NatsClientReactiveE2ETests(NatsTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    static NatsOpts CreateOpts(string url) => new() { Url = url };

    [Fact]
    public async Task NatsSubscribe_Ping_receives_message()
    {
        await using var connection = new NatsConnection(CreateOpts(fixture.Server.Url));
        var hub = NatsService.For<IE2EHubReactive>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        await NatsE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await connection.PublishAsync("e2e.ping", "hello", cancellationToken: ct);
            },
            receive,
            cts.Token);

        Assert.Equal("hello", await receive);
    }

    [Fact]
    public async Task NatsPublish_PublishPing_reaches_subscriber()
    {
        await using var subscriber = new NatsConnection(CreateOpts(fixture.Server.Url));
        await using var publisher = new NatsConnection(CreateOpts(fixture.Server.Url));
        var subHub = NatsService.For<IE2EHubReactive>(subscriber);
        var pubHub = NatsService.For<IE2EHubReactive>(publisher);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = subHub.Ping.Timeout(DefaultTimeout).FirstAsync().ToTask(cts.Token);
        await NatsE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPing().Timeout(DefaultTimeout).FirstAsync().ToTask(ct);
            },
            receive,
            cts.Token);

        var result = await receive;
        Assert.True(string.IsNullOrEmpty(result));
    }
}
