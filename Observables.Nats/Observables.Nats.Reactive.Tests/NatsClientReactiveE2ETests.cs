using NATS.Client.Core;
using Observables.Nats;
using Observables.Nats.Reactive;
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

    [Fact]
    public async Task FromSubscribe_dispose_cancels_the_pump_without_completing()
    {
        await using var subscriber = new NatsConnection(CreateOpts(fixture.Server.Url));
        await using var publisher = new NatsConnection(CreateOpts(fixture.Server.Url));
        var subject = "e2e.dispose." + Guid.NewGuid().ToString("N");

        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveNatsAdapter.FromSubscribe<string>(subscriber, subject)
            .Subscribe(
                value => ready.TrySetResult(value),
                _ => Interlocked.Exchange(ref errored, 1),
                () => Interlocked.Exchange(ref completed, 1));

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await NatsE2EHelpers.PublishUntilReceivedAsync(
            async ct =>
            {
                await publisher.PublishAsync(subject, "ready", cancellationToken: ct);
            },
            ready.Task,
            cts.Token);

        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await Task.Delay(200, cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
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
                async ct => await SystemReactiveNatsAdapter.FromRequest<string, int>(client, subject, "q", ct)
                    .Timeout(DefaultTimeout)
                    .FirstAsync()
                    .ToTask(ct),
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
            async ct => await SystemReactiveNatsAdapter.FromRequest<string, int>(client, subject, "q", ct)
                .Timeout(DefaultTimeout)
                .FirstAsync()
                .ToTask(ct),
            cts.Token);

        await respondTask;
        Assert.Equal(7, reply);
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
