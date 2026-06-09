using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Observables.Grpc.Tests.Infrastructure;

/// <summary>Minimal in-process gRPC host for E2E tests.</summary>
public sealed class GrpcTestHost : IAsyncDisposable
{
    readonly IHost host;

    GrpcTestHost(IHost host, string address)
    {
        this.host = host;
        Address = address;
    }

    public string Address { get; }

    public static async Task<GrpcTestHost> StartAsync()
    {
        var port = Random.Shared.Next(50_000, 60_000);
        var address = $"http://127.0.0.1:{port}";

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(options =>
                {
                    options.ListenLocalhost(port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                });
                webBuilder.ConfigureServices(services => services.AddGrpc());
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGrpcService<EchoServiceImpl>());
                });
            });

        var host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);
        return new GrpcTestHost(host, address);
    }

    public async ValueTask DisposeAsync() => await host.StopAsync().ConfigureAwait(false);
}
