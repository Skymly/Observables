using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Observables.Grpc.Tests.Infrastructure;

/// <summary>Hosts gRPC services in-memory via <see cref="TestServer"/>.</summary>
public sealed class GrpcTestHost : IAsyncDisposable
{
    readonly IHost host;
    readonly TestServer testServer;

    GrpcTestHost(IHost host, TestServer testServer)
    {
        this.host = host;
        this.testServer = testServer;
        Address = testServer.BaseAddress.ToString().TrimEnd('/');
    }

    public string Address { get; }

    public HttpMessageHandler CreateHandler() => testServer.CreateHandler();

    public static async Task<GrpcTestHost> StartAsync()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddGrpc());
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGrpcService<EchoServiceImpl>());
                });
            })
            .Build();

        await host.StartAsync().ConfigureAwait(false);
        return new GrpcTestHost(host, host.GetTestServer());
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync().ConfigureAwait(false);
        host.Dispose();
    }
}
