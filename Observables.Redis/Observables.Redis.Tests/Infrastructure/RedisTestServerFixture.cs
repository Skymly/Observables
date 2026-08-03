namespace Observables.Redis.Tests.Infrastructure;

public sealed class RedisTestServerFixture : IAsyncLifetime
{
    public RedisTestServer Server { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Server = await RedisTestServer.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Server is not null)
        {
            await Server.DisposeAsync().ConfigureAwait(false);
        }
    }
}

[CollectionDefinition(nameof(RedisTestServerCollection), DisableParallelization = true)]
public sealed class RedisTestServerCollection : ICollectionFixture<RedisTestServerFixture>;
