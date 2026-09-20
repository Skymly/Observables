using Npgsql;
using Observables.Postgres.Tests.Infrastructure;
using R3;

namespace Observables.Postgres.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresFromListenContractTests(PostgresTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task FromListen_dispose_unlistens_and_releases_connection()
    {
        const string channel = "r3_listen_dispose";
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);

        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = PostgresObservable.FromListen(listener, channel)
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
        await NotifyAsync(notifier, channel, "wake", timeout.Token);
        await WaitUntilIdleAndNotListeningAsync(listener, channel, timeout.Token);
    }


    [Fact]
    public async Task FromListen_second_listen_on_same_connection_is_rejected()
    {
        const string channel = "r3_listen_exclusive";
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);

        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = PostgresObservable.FromListen(listener, channel)
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

        var observer = new RecordingObserver<string>();
        using var second = PostgresObservable.FromListen(listener, channel).Subscribe(observer);
        var result = await observer.Completed.WaitAsync(timeout.Token);
        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("second LISTEN", result.Exception.Message, StringComparison.Ordinal);
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
        var sawInProgress = false;
        long lastCount = -1;
        while (!cancellation.IsCancellationRequested)
        {
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
                lastCount = Convert.ToInt64(await command.ExecuteScalarAsync(cancellation));
                if (lastCount == 0)
                {
                    return;
                }
            }
            catch (NpgsqlOperationInProgressException)
            {
                sawInProgress = true;
            }

            try
            {
                await Task.Delay(50, cancellation);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Fail(
            $"Connection still listening on '{channel}' or WaitAsync is still in progress. lastCount={lastCount}, sawInProgress={sawInProgress}.");
    }

    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Result> Completed => _completed.Task;

        protected override void OnNextCore(T value)
        {
        }

        protected override void OnErrorResumeCore(Exception error)
        {
        }

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }
}
