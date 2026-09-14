using Npgsql;
using Observables.Postgres.Tests.Infrastructure;

namespace Observables.Postgres.Tests;

[Collection(nameof(PostgresTestServerCollection))]
public sealed class PostgresUnlistenTests(PostgresTestServerFixture fixture)
{
    [Fact]
    public async Task Unlisten_propagates_failures_that_are_not_in_progress()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var victim = new NpgsqlConnection(fixture.Server.ConnectionString);
        await victim.OpenAsync(cancellation);
        int pid;
        await using (var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid();", victim))
        {
            pid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync(cancellation));
        }

        await using var killer = new NpgsqlConnection(fixture.Server.ConnectionString);
        await killer.OpenAsync(cancellation);
        await using (var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid);", killer))
        {
            terminate.Parameters.AddWithValue("pid", pid);
            await terminate.ExecuteNonQueryAsync(cancellation);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => PostgresProtocol.UnlistenAsync(victim, "unlisten_fail"));
    }
}
