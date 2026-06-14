using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace Observables.Nats.Tests.Infrastructure;

/// <summary>In-process NATS server for E2E tests (downloads portable nats-server when missing).</summary>
public sealed class NatsTestServer : IAsyncDisposable
{
    const string NatsServerVersion = "v2.10.28";
    readonly Process process;
    readonly string url;

    NatsTestServer(Process process, string url)
    {
        this.process = process;
        this.url = url;
    }

    public string Url => url;

    public static async Task<NatsTestServer> StartAsync(CancellationToken cancellationToken = default)
    {
        var port = ReserveFreeTcpPort();
        var serverPath = await EnsureNatsServerPathAsync(cancellationToken).ConfigureAwait(false);
        var configPath = Path.Combine(Path.GetTempPath(), $"observables-nats-{port}.conf");
        await File.WriteAllTextAsync(
            configPath,
            $"port: {port}\n",
            cancellationToken).ConfigureAwait(false);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = $"-c \"{configPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start nats-server.");
        }

        var url = $"nats://127.0.0.1:{port}";
        await WaitForPortAsync(port, cancellationToken).ConfigureAwait(false);
        return new NatsTestServer(process, url);
    }

    static async Task<string> EnsureNatsServerPathAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "observables-nats-test", NatsServerVersion);
        var exeName = OperatingSystem.IsWindows() ? "nats-server.exe" : "nats-server";
        var exePath = Path.Combine(root, exeName);
        if (File.Exists(exePath))
        {
            return exePath;
        }

        Directory.CreateDirectory(root);
        var asset = OperatingSystem.IsWindows()
            ? $"nats-server-{NatsServerVersion}-windows-amd64.zip"
            : OperatingSystem.IsLinux()
                ? $"nats-server-{NatsServerVersion}-linux-amd64.zip"
                : throw new PlatformNotSupportedException("E2E NATS tests require Windows or Linux CI agents.");

        var zipPath = Path.Combine(root, asset);
        if (!File.Exists(zipPath))
        {
            var downloadUrl =
                $"https://github.com/nats-io/nats-server/releases/download/{NatsServerVersion}/{asset}";
            using var client = new HttpClient();
            await using var stream = await client
                .GetStreamAsync(downloadUrl, cancellationToken)
                .ConfigureAwait(false);
            await using var file = File.Create(zipPath);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        ZipFile.ExtractToDirectory(zipPath, root, overwriteFiles: true);
        var extracted = Directory.GetFiles(root, exeName, SearchOption.AllDirectories).FirstOrDefault();
        if (extracted is not null && !string.Equals(extracted, exePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(extracted, exePath, overwrite: true);
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("nats-server executable not found after extract.", exePath);
        }

        return exePath;
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

    static async Task WaitForPortAsync(int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        for (var i = 0; i < 50; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"nats-server did not listen on port {port}.");
    }

    public async ValueTask DisposeAsync()
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        process.Dispose();
    }
}
