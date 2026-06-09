namespace Observables.Grpc.Tests.Infrastructure;

[CollectionDefinition(nameof(GrpcTestHostCollection))]
public sealed class GrpcTestHostCollection : ICollectionFixture<GrpcTestHostFixture>;

public sealed class GrpcTestHostFixture : IAsyncLifetime
{
    public GrpcTestHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync() => Host = await GrpcTestHost.StartAsync();

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}
