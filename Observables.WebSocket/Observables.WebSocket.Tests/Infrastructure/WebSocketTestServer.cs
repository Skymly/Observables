using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Observables.WebSocket.Tests.Infrastructure;

/// <summary>Minimal in-process WebSocket echo server for E2E tests.</summary>
public sealed class WebSocketTestServer : IAsyncDisposable
{
    readonly HttpListener listener;
    readonly CancellationTokenSource cts = new();
    readonly Task acceptLoop;

    WebSocketTestServer(HttpListener listener, int port)
    {
        this.listener = listener;
        Port = port;
        acceptLoop = AcceptLoopAsync(cts.Token);
    }

    public int Port { get; }

    public static WebSocketTestServer Start()
    {
        var port = ReserveFreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return new WebSocketTestServer(listener, port);
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

    public Uri Uri => new($"ws://127.0.0.1:{Port}/");

    async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            _ = HandleClientAsync(ctx, cancellationToken);
        }
    }

    static async Task HandleClientAsync(HttpListenerContext ctx, CancellationToken cancellationToken)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
        var ws = wsCtx.WebSocket;
        var buffer = new byte[4096];

        try
        {
            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Assemble fragmented message before echoing
                using var ms = new MemoryStream();
                WebSocketMessageType messageType = WebSocketMessageType.Binary;
                WebSocketReceiveResult result;
                do
                {
                    result = await ws
                        .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws
                            .CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    messageType = result.MessageType;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                // Echo back as single frame
                var payload = ms.ToArray();
                await ws
                    .SendAsync(new ArraySegment<byte>(payload), messageType, true, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        listener.Stop();
        try { await acceptLoop.ConfigureAwait(false); } catch { }
        cts.Dispose();
    }
}
