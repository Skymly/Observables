namespace Observables.SignalR.Tests.Infrastructure;

public sealed class SignalRTestServerFixture : IAsyncLifetime, IAsyncDisposable
{
    public SignalRTestServer Server { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Server = await SignalRTestServer.StartAsync();

    public async ValueTask DisposeAsync() => await Server.DisposeAsync().ConfigureAwait(false);
}

[CollectionDefinition(nameof(SignalRTestServerCollection), DisableParallelization = true)]
public sealed class SignalRTestServerCollection : ICollectionFixture<SignalRTestServerFixture>;
