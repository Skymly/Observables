namespace Observables.Sse.Tests.Infrastructure;

[CollectionDefinition(nameof(SseTestServerCollection))]
public sealed class SseTestServerCollection : ICollectionFixture<SseTestServerFixture>;

public sealed class SseTestServerFixture : IAsyncLifetime
{
    public SseTestServer Server { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Server = SseTestServer.Start();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await Server.DisposeAsync();
}
