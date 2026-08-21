using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Observables.Sse.Tests.Infrastructure;

/// <summary>Minimal in-process <c>text/event-stream</c> server for E2E tests.</summary>
public sealed class SseTestServer : IAsyncDisposable
{
    // `/stream` writes one deterministic event block then closes. `/keepalive` emits one event and holds the response open.
    const string Stream =
        ": comment line\n" +
        "event: price\n" +
        "data: 100\n" +
        "\n" +
        "event: price\n" +
        "data: 200\n" +
        "\n" +
        "event: tick\n" +
        "data: {\"value\":42}\n" +
        "\n" +
        "data: beat\n" +
        "\n";

    readonly HttpListener listener;
    readonly CancellationTokenSource cts = new();
    readonly Task acceptLoop;

    SseTestServer(HttpListener listener, int port)
    {
        this.listener = listener;
        Port = port;
        acceptLoop = AcceptLoopAsync(cts.Token);
    }

    public int Port { get; }

    public Uri Uri => new($"http://127.0.0.1:{Port}/stream");

    public Uri KeepAliveUri => new($"http://127.0.0.1:{Port}/keepalive");

    public static SseTestServer Start()
    {
        var port = ReserveFreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return new SseTestServer(listener, port);
    }

    static int ReserveFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = HandleAsync(context, cancellationToken);
        }
    }

    static async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.StatusCode = 200;

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.Equals("/keepalive", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.SendChunked = true;
                var ready = Encoding.UTF8.GetBytes(
                    "event: price\n" +
                    "data: ready\n" +
                    "\n");
                await context.Response.OutputStream.WriteAsync(ready, 0, ready.Length).ConfigureAwait(false);
                await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(Stream);
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // best-effort test server
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
                // ignore
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        try
        {
            await acceptLoop.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        cts.Dispose();
    }
}
