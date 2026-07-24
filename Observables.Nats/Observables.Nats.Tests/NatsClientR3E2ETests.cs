using NATS.Client.Core;
using Observables.Nats;
using Observables.Nats.Tests.Contracts;
using Observables.Nats.Tests.Infrastructure;
using R3;

namespace Observables.Nats.Tests;

[Collection(nameof(NatsTestServerCollection))]
public sealed class NatsClientR3E2ETests(NatsTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    static NatsOpts CreateOpts(string url) => new() { Url = url };

    [Fact]
    public async Task NatsSubscribe_Ping_receives_message()
    {
        await using var connection = new NatsConnection(CreateOpts(fixture.Server.Url));
        var hub = NatsService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(cts.Token);
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
        var subHub = NatsService.For<IE2EHub>(subscriber);
        var pubHub = NatsService.For<IE2EHub>(publisher);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = subHub.Ping.FirstAsync(cts.Token);
        await NatsE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await pubHub.PublishPing().FirstAsync(ct);
            },
            receive,
            cts.Token);

        var result = await receive;
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task NatsRequest_Echo_roundtrip()
    {
        await using var responder = new NatsConnection(CreateOpts(fixture.Server.Url));
        await using var client = new NatsConnection(CreateOpts(fixture.Server.Url));

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var respondTask = RespondEchoAsync(responder, cts.Token);

        var hub = NatsService.For<IE2EHub>(client);
        var reply = await hub.Echo("hello").FirstAsync(cts.Token);

        await respondTask;
        Assert.Equal("hello", reply);
    }

    static async Task RespondEchoAsync(INatsConnection connection, CancellationToken cancellationToken)
    {
        await foreach (var msg in connection.SubscribeAsync<string>("e2e.echo", cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            await msg.ReplyAsync(msg.Data, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }
    }
}
