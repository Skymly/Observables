using Npgsql;
using Observables.Postgres;
using Observables.Postgres.Tests.Contracts;
using Observables.Postgres.Tests.Infrastructure;
using R3;

namespace Observables.Postgres.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresClientR3E2ETests(PostgresTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Listen_receives_Notify_from_another_session()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);
        var hub = PostgresService.For<IE2EHub>(listener);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);
        var receive = hub.Ping.FirstAsync(timeout.Token);

        await using var notifier = new NpgsqlConnection(fixture.Server.ConnectionString);
        await notifier.OpenAsync(cancellation);
        await using (var notify = new NpgsqlCommand("SELECT pg_notify('e2e_ping', 'hello-from-peer');", notifier))
        {
            // Retry briefly: Listen may still be executing LISTEN when the first NOTIFY fires.
            for (var attempt = 0; attempt < 20 && !receive.IsCompleted; attempt++)
            {
                await notify.ExecuteNonQueryAsync(cancellation);
                await Task.Delay(50, cancellation);
            }
        }

        Assert.Equal("hello-from-peer", await receive);
    }

    [Fact]
    public async Task Notify_from_proxy_is_observed_by_second_Listen_connection()
    {
        var cancellation = TestContext.Current.CancellationToken;
        const string channel = "e2e_ping";
        const string payload = "hello-from-proxy";

        await using var observer = new NpgsqlConnection(fixture.Server.ConnectionString);
        await observer.OpenAsync(cancellation);

        var notification = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.Notification += (_, args) =>
        {
            if (string.Equals(args.Channel, channel, StringComparison.Ordinal))
            {
                notification.TrySetResult(args.Payload);
            }
        };

        await using (var listen = new NpgsqlCommand($"LISTEN \"{channel}\";", observer))
        {
            await listen.ExecuteNonQueryAsync(cancellation);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(DefaultTimeout);
        var waitTask = observer.WaitAsync(timeout.Token);

        await using var publisher = new NpgsqlConnection(fixture.Server.ConnectionString);
        await publisher.OpenAsync(cancellation);
        var hub = PostgresService.For<IE2EHub>(publisher);
        await hub.PublishPing(payload).FirstAsync(timeout.Token);

        var received = await notification.Task.WaitAsync(timeout.Token);
        await waitTask;

        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task Generated_hub_notify_fails_while_listen_holds_the_same_connection()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(fixture.Server.ConnectionString);
        await connection.OpenAsync(cancellation);
        var hub = PostgresService.For<IE2EHub>(connection);

        using var listen = hub.Ping.Subscribe(_ => { });
        await Task.Delay(300, cancellation);

        var observer = new RecordingObserver<Unit>();
        using var notify = hub.PublishPing("same-connection").Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), cancellation);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Contains("LISTEN", result.Exception.Message, StringComparison.Ordinal);
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
