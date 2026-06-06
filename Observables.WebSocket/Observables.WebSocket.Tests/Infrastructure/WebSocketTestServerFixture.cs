namespace Observables.WebSocket.Tests.Infrastructure;

[CollectionDefinition(nameof(WebSocketTestServerCollection))]
public sealed class WebSocketTestServerCollection : ICollectionFixture<WebSocketTestServerFixture>;

public sealed class WebSocketTestServerFixture : IAsyncLifetime
{
    public WebSocketTestServer Server { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Server = WebSocketTestServer.Start();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await Server.DisposeAsync();
}
