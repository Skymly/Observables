using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace Observables.Nats.Tests.Infrastructure;

/// <summary>In-process NATS server for E2E tests (downloads portable nats-server when missing).</summary>
public sealed class NatsTestServer : IAsyncDisposable
{
    const string NatsServerVersion = "v2.10.28";
    static readonly SemaphoreSlim ServerBinaryGate = new(1, 1);
    static readonly Semaphore CrossProcessServerBinaryGate = CreateCrossProcessGate();
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

        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start nats-server.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var url = $"nats://127.0.0.1:{port}";
        await WaitForPortAsync(port, cancellationToken).ConfigureAwait(false);
        return new NatsTestServer(process, url);
    }

    static Task<string> EnsureNatsServerPathAsync(CancellationToken cancellationToken) =>
        WithCrossProcessBinaryGateAsync(
            () => EnsureNatsServerPathCoreAsync(cancellationToken),
            cancellationToken);

    internal static async Task<T> WithCrossProcessBinaryGateAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await ServerBinaryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WaitForCrossProcessGate();
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                CrossProcessServerBinaryGate.Release();
            }
        }
        finally
        {
            ServerBinaryGate.Release();
        }
    }

    static Semaphore CreateCrossProcessGate()
    {
        var name = "Observables.NatsTestServer.bin." + NatsServerVersion;
        try
        {
            return new Semaphore(1, 1, @"Global\" + name);
        }
        catch (UnauthorizedAccessException)
        {
            return new Semaphore(1, 1, @"Local\" + name);
        }
    }

    static void WaitForCrossProcessGate()
    {
        CrossProcessServerBinaryGate.WaitOne();
    }

    static async Task<string> EnsureNatsServerPathCoreAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "observables-nats-test", NatsServerVersion);
        var exeName = OperatingSystem.IsWindows() ? "nats-server.exe" : "nats-server";
        var exePath = Path.Combine(root, exeName);
        if (File.Exists(exePath))
        {
            EnsureExecutable(exePath);
            return exePath;
        }

        Directory.CreateDirectory(root);
        var zip = OperatingSystem.IsWindows();
        var archive = zip
            ? $"nats-server-{NatsServerVersion}-windows-amd64.zip"
            : OperatingSystem.IsLinux()
                ? $"nats-server-{NatsServerVersion}-linux-amd64.tar.gz"
                : throw new PlatformNotSupportedException("E2E NATS tests require Windows or Linux CI agents.");

        var archivePath = Path.Combine(root, archive);
        if (!IsUsableArchive(archivePath, zip))
        {
            TryDelete(archivePath);
            var downloadUrl =
                $"https://github.com/nats-io/nats-server/releases/download/{NatsServerVersion}/{archive}";
            using var client = new HttpClient();
            await using var stream = await client
                .GetStreamAsync(downloadUrl, cancellationToken)
                .ConfigureAwait(false);
            await DownloadAtomicallyAsync(stream, archivePath, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (zip)
            {
                ZipFile.ExtractToDirectory(archivePath, root, overwriteFiles: true);
            }
            else
            {
                using var extraction = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "tar",
                        UseShellExecute = false,
                        ArgumentList =
                        {
                            "-xzf",
                            archivePath,
                            "-C",
                            root,
                        },
                    }) ?? throw new InvalidOperationException("Failed to start tar for NATS server extraction.");
                await extraction.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (extraction.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"tar failed to extract NATS server archive with exit code {extraction.ExitCode}.");
                }
            }
        }
        catch
        {
            TryDelete(archivePath);
            throw;
        }

        var extracted = Directory.GetFiles(root, exeName, SearchOption.AllDirectories).FirstOrDefault();
        if (extracted is not null && !string.Equals(extracted, exePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(extracted, exePath, overwrite: true);
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("nats-server executable not found after extract.", exePath);
        }

        EnsureExecutable(exePath);
        return exePath;
    }

    internal static bool IsUsableArchive(string archivePath, bool zip)
    {
        if (!File.Exists(archivePath) || new FileInfo(archivePath).Length == 0)
        {
            return false;
        }

        if (!zip)
        {
            return new FileInfo(archivePath).Length > 1024;
        }

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return archive.Entries.Count > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static async Task DownloadAtomicallyAsync(
        Stream source,
        string archivePath,
        CancellationToken cancellationToken)
    {
        var partialPath = archivePath + ".partial";
        try
        {
            await using (var file = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            File.Move(partialPath, archivePath, overwrite: true);
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }

    static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(
            path,
            mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
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

    internal static async Task WaitForPortAsync(int port, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 50; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
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
