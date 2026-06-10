using System.Net;
using System.Text;

namespace Observables.Sse.Tests.Infrastructure;

/// <summary>Minimal in-process <c>text/event-stream</c> server for E2E tests.</summary>
public sealed class SseTestServer : IAsyncDisposable
{
    // One deterministic event block written per request, then the response closes.
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

    public static SseTestServer Start()
    {
        var port = Random.Shared.Next(50_000, 60_000);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return new SseTestServer(listener, port);
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

            _ = HandleAsync(context);
        }
    }

    static async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.StatusCode = 200;
            var bytes = Encoding.UTF8.GetBytes(Stream);
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
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
