namespace Observables.Nats.Tests.Infrastructure;

public sealed class NatsTestServerFixture : IAsyncLifetime, IAsyncDisposable
{
    public NatsTestServer Server { get; private set; } = null!;

    public async ValueTask InitializeAsync() =>
        Server = await NatsTestServer.StartAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await Server.DisposeAsync().ConfigureAwait(false);
}

[CollectionDefinition(nameof(NatsTestServerCollection))]
public sealed class NatsTestServerCollection : ICollectionFixture<NatsTestServerFixture>;
