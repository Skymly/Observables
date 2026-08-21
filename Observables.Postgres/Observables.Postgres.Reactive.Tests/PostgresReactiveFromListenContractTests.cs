using System.Reactive.Linq;
using System.Text.Json;
using Npgsql;
using Observables.Postgres.Reactive;
using Observables.Postgres.Reactive.Tests.Contracts;
using Observables.Postgres.Tests.Infrastructure;

namespace Observables.Postgres.Reactive.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresReactiveFromListenContractTests(PostgresTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Typed_FromListen_deserialize_failure_stops_the_stream()
    {
        const string channel = "rx_listen_onerror";
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);

        var nextCount = 0;
        var completed = 0;
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactivePostgresAdapter.FromListen<OrderPayload>(listener, channel)
            .Subscribe(
                _ => Interlocked.Increment(ref nextCount),
                ex => error.TrySetResult(ex),
                () => Interlocked.Exchange(ref completed, 1));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);

        await using var notifier = new NpgsqlConnection(fixture.Server.ConnectionString);
        await notifier.OpenAsync(cancellation);
        await NotifyUntilAsync(
            notifier,
            channel,
            "not-json",
            () => error.Task.IsCompleted,
            timeout.Token);

        var deserializeError = await error.Task.WaitAsync(timeout.Token);
        Assert.IsType<JsonException>(deserializeError);

        var valid = JsonSerializer.Serialize(new OrderPayload { OrderId = "after-error", Quantity = 1 });
        await NotifyAsync(notifier, channel, valid, timeout.Token);
        await Task.Delay(200, timeout.Token);

        Assert.Equal(0, Volatile.Read(ref nextCount));
        Assert.Equal(0, Volatile.Read(ref completed));

        await WaitUntilIdleAndNotListeningAsync(listener, channel, timeout.Token);
    }

    [Fact]
    public async Task FromListen_dispose_unlistens_and_releases_connection()
    {
        const string channel = "rx_listen_dispose";
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);

        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = SystemReactivePostgresAdapter.FromListen(listener, channel)
            .Subscribe(payload => ready.TrySetResult(payload));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);

        await using var notifier = new NpgsqlConnection(fixture.Server.ConnectionString);
        await notifier.OpenAsync(cancellation);
        await NotifyUntilAsync(
            notifier,
            channel,
            "ready",
            () => ready.Task.IsCompleted,
            timeout.Token);

        Assert.Equal("ready", await ready.Task.WaitAsync(timeout.Token));

        subscription.Dispose();
        await WaitUntilIdleAndNotListeningAsync(listener, channel, timeout.Token);
    }

    static async Task NotifyUntilAsync(
        NpgsqlConnection notifier,
        string channel,
        string payload,
        Func<bool> completed,
        CancellationToken cancellation)
    {
        for (var attempt = 0; attempt < 20 && !completed(); attempt++)
        {
            await NotifyAsync(notifier, channel, payload, cancellation);
            await Task.Delay(50, cancellation);
        }
    }

    static async Task NotifyAsync(
        NpgsqlConnection connection,
        string channel,
        string payload,
        CancellationToken cancellation)
    {
        await using var notify = new NpgsqlCommand("SELECT pg_notify(@c, @p);", connection)
        {
            Parameters =
            {
                new("c", channel),
                new("p", payload),
            },
        };
        await notify.ExecuteNonQueryAsync(cancellation);
    }

    static async Task WaitUntilIdleAndNotListeningAsync(
        NpgsqlConnection connection,
        string channel,
        CancellationToken cancellation)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM pg_listening_channels() AS t(channel) WHERE t.channel = @channel;",
                    connection)
                {
                    Parameters =
                    {
                        new("channel", channel),
                    },
                };
                var count = (long)(await command.ExecuteScalarAsync(cancellation))!;
                if (count == 0)
                {
                    return;
                }
            }
            catch (NpgsqlOperationInProgressException)
            {
            }

            await Task.Delay(50, cancellation);
        }

        Assert.Fail($"Connection still listening on '{channel}' or WaitAsync is still in progress.");
    }
}
