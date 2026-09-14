using System.Net;
using System.Net.Sockets;
using Garnet;
using StackExchange.Redis;

namespace Observables.Redis.Tests.Infrastructure;

/// <summary>
/// In-process Microsoft.Garnet server for Redis Pub/Sub E2E (locked by #170).
/// Test/tooling only — Garnet must not enter pack dependency graphs.
/// </summary>
public sealed class RedisTestServer : IAsyncDisposable
{
    readonly string _workDir;
    readonly GarnetServer _server;

    RedisTestServer(string workDir, int port, GarnetServer server)
    {
        _workDir = workDir;
        Port = port;
        _server = server;
        Endpoint = $"127.0.0.1:{port}";
    }

    public int Port { get; }

    public string Endpoint { get; }

    public static async Task<RedisTestServer> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workDir = Path.Combine(Path.GetTempPath(), "observables-redis-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var port = ReserveFreeTcpPort();

        var server = new GarnetServer(
            [
                "--bind", "127.0.0.1",
                "--port", port.ToString(),
                "--memory", "16m",
                "--page", "8k",
                "--segment", "1m",
                "--index", "8m",
                "--checkpointdir", workDir,
                "--logger-level", "Error",
                "--disable-console-logger", "true",
            ],
            cleanupDir: true);

        server.Start();
        await WaitUntilTcpPortAcceptsAsync(port, cancellationToken).ConfigureAwait(false);
        return new RedisTestServer(workDir, port, server);
    }

    public static async Task WaitUntilTcpPortAcceptsAsync(
        int port,
        CancellationToken cancellationToken,
        int attempts = 50,
        int delayMilliseconds = 50)
    {
        if (attempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts));
        }

        SocketException? lastError = null;
        for (var i = 0; i < attempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(delayMilliseconds);
            using var client = new TcpClient();
            var spentAttemptBudget = false;
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, attemptCts.Token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                spentAttemptBudget = true;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException ex)
            {
                lastError = ex;
            }

            if (i == attempts - 1)
            {
                break;
            }

            if (!spentAttemptBudget)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"TCP port {port} did not accept connections.", lastError);
    }

    public async Task<ConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var config = ConfigurationOptions.Parse(Endpoint);
        config.AbortOnConnectFail = true;
        config.ConnectTimeout = 5000;
        config.SyncTimeout = 5000;
        config.AsyncTimeout = 5000;
        config.AllowAdmin = false;
        return await ConnectionMultiplexer.ConnectAsync(config).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _server.Dispose();
        }
        catch
        {
            // best-effort
        }

        try
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }

        await Task.CompletedTask.ConfigureAwait(false);
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
}
