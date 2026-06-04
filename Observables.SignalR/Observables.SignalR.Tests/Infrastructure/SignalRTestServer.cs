using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Observables.SignalR.Tests.Infrastructure;

/// <summary>Hosts <see cref="E2ETestHub"/> in-memory via <see cref="TestServer"/>.</summary>
public sealed class SignalRTestServer : IAsyncDisposable
{
    readonly IHost host;
    readonly TestServer testServer;

    SignalRTestServer(IHost host, TestServer testServer)
    {
        this.host = host;
        this.testServer = testServer;
        HubUri = new Uri(testServer.BaseAddress, "hub");
    }

    public Uri HubUri { get; }

    public static async Task<SignalRTestServer> StartAsync(CancellationToken cancellationToken = default)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                    services.AddSignalR(options => options.EnableDetailedErrors = true));
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapHub<E2ETestHub>("/hub"));
                });
            })
            .Build();

        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        return new SignalRTestServer(host, host.GetTestServer());
    }

    public HubConnection CreateConnection() =>
        new HubConnectionBuilder()
            .WithUrl(HubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
            })
            .Build();

    public HubConnection CreateStreamingConnection()
    {
        var webSocketClient = testServer.CreateWebSocketClient();
        return new HubConnectionBuilder()
            .WithUrl(HubUri, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await webSocketClient.ConnectAsync(context.Uri, cancellationToken).ConfigureAwait(false);
            })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync().ConfigureAwait(false);
        host.Dispose();
    }
}
