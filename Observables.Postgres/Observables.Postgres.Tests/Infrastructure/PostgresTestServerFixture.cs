namespace Observables.Postgres.Tests.Infrastructure;

public sealed class PostgresTestServerFixture : IAsyncLifetime, IAsyncDisposable
{
    public PostgresTestServer Server { get; private set; } = null!;

    public async ValueTask InitializeAsync() =>
        Server = await PostgresTestServer.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await Server.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// Shares one B-tier PostgreSQL peer; disables parallelization so concurrent tests do not
/// contend on LISTEN/NOTIFY sessions or the download gate.
/// </summary>
[CollectionDefinition(nameof(PostgresTestServerCollection), DisableParallelization = true)]
public sealed class PostgresTestServerCollection : ICollectionFixture<PostgresTestServerFixture>;
