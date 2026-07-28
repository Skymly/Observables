using Npgsql;
using Observables.Postgres.Tests.Infrastructure;

namespace Observables.Postgres.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresListenNotifyPeerTests(PostgresTestServerFixture fixture)
{
    [Fact]
    public async Task Listen_receives_Notify_across_two_connections()
    {
        const string channel = "observables_peer";
        const string payload = "hello-from-notify";
        var cancellation = TestContext.Current.CancellationToken;

        await using var listener = new NpgsqlConnection(fixture.Server.ConnectionString);
        await listener.OpenAsync(cancellation);

        var notification = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.Notification += (_, args) =>
        {
            if (string.Equals(args.Channel, channel, StringComparison.Ordinal))
            {
                notification.TrySetResult(args.Payload);
            }
        };

        await using (var listenCommand = new NpgsqlCommand($"LISTEN {channel};", listener))
        {
            await listenCommand.ExecuteNonQueryAsync(cancellation);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var waitTask = listener.WaitAsync(timeout.Token);

        await using var notifier = new NpgsqlConnection(fixture.Server.ConnectionString);
        await notifier.OpenAsync(cancellation);
        await using (var notifyCommand = new NpgsqlCommand($"NOTIFY {channel}, '{payload}';", notifier))
        {
            await notifyCommand.ExecuteNonQueryAsync(cancellation);
        }

        var received = await notification.Task.WaitAsync(timeout.Token);
        await waitTask;

        Assert.Equal(payload, received);
    }
}
