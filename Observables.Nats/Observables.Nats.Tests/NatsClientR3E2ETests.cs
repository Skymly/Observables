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
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respondTask = RespondEchoAsync(responder, subscribed, cts.Token);
        await subscribed.Task.WaitAsync(cts.Token);

        var hub = NatsService.For<IE2EHub>(client);
        var reply = await NatsE2EHelpers.RequestUntilRespondedAsync(
            async ct => await hub.Echo("hello").FirstAsync(ct),
            cts.Token);

        await respondTask;
        Assert.Equal("hello", reply);
    }

    [Fact]
    public async Task NatsRequest_malformed_int_reply_fails()
    {
        await using var responder = new NatsConnection(CreateOpts(fixture.Server.Url));
        await using var client = new NatsConnection(CreateOpts(fixture.Server.Url));
        var subject = "e2e.int." + Guid.NewGuid().ToString("N");

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respondTask = RespondRawAsync(responder, subject, "not-json", subscribed, cts.Token);
        await subscribed.Task.WaitAsync(cts.Token);

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            async () => await NatsE2EHelpers.RequestUntilRespondedAsync(
                async ct => await NatsObservable.FromRequest<string, int>(client, subject, "q", ct).FirstAsync(ct),
                cts.Token));

        await respondTask;
        Assert.IsNotType<NatsNoRespondersException>(ex);
    }

    [Fact]
    public async Task NatsRequest_well_formed_int_reply_succeeds()
    {
        await using var responder = new NatsConnection(CreateOpts(fixture.Server.Url));
        await using var client = new NatsConnection(CreateOpts(fixture.Server.Url));
        var subject = "e2e.intok." + Guid.NewGuid().ToString("N");

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respondTask = RespondRawAsync(responder, subject, "7", subscribed, cts.Token);
        await subscribed.Task.WaitAsync(cts.Token);

        var reply = await NatsE2EHelpers.RequestUntilRespondedAsync(
            async ct => await NatsObservable.FromRequest<string, int>(client, subject, "q", ct).FirstAsync(ct),
            cts.Token);

        await respondTask;
        Assert.Equal(7, reply);
    }

    static async Task RespondEchoAsync(
        INatsConnection connection,
        TaskCompletionSource subscribed,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var sub = await connection
                .SubscribeCoreAsync<string>("e2e.echo", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await connection.PingAsync(cancellationToken).ConfigureAwait(false);
            subscribed.TrySetResult();

            var msg = await sub.Msgs.ReadAsync(cancellationToken).ConfigureAwait(false);
            await msg.ReplyAsync(msg.Data, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            subscribed.TrySetException(ex);
            throw;
        }
    }

    static async Task RespondRawAsync(
        INatsConnection connection,
        string subject,
        string payload,
        TaskCompletionSource subscribed,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var sub = await connection
                .SubscribeCoreAsync<string>(subject, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await connection.PingAsync(cancellationToken).ConfigureAwait(false);
            subscribed.TrySetResult();

            var msg = await sub.Msgs.ReadAsync(cancellationToken).ConfigureAwait(false);
            await msg.ReplyAsync(payload, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            subscribed.TrySetException(ex);
            throw;
        }
    }
}
