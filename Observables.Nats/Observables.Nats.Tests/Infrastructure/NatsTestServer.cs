using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

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
        var serverPath = await EnsureNatsServerPathAsync(cancellationToken).ConfigureAwait(false);
        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var port = ReserveFreeTcpPort();
            var configPath = Path.Combine(Path.GetTempPath(), $"observables-nats-{port}.conf");
            await File.WriteAllTextAsync(
                configPath,
                $"listen: 127.0.0.1:{port}\n",
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

            try
            {
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                TryStop(process);
                TryDelete(configPath);
            }
        }

        throw new InvalidOperationException("Failed to start nats-server.", last);
    }

    static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            process.Dispose();
        }
        catch
        {
            // ignored
        }
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
            using var fileLock = OperatingSystem.IsWindows() ? null : AcquireFileLock();
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
        if (!OperatingSystem.IsWindows())
        {
            // Named semaphores are Windows-only; unnamed Semaphore is not thread-affine, so await+Release is safe.
            return new Semaphore(1, 1);
        }

        var name = "Observables.NatsTestServer.bin." + NatsServerVersion;
        try
        {
            return new Semaphore(1, 1, @"Global\" + name);
        }
        catch (UnauthorizedAccessException)
        {
            return new Semaphore(1, 1, @"Local\" + name);
        }
        catch (PlatformNotSupportedException)
        {
            return new Semaphore(1, 1);
        }
    }

    static FileStream AcquireFileLock()
    {
        var directory = Path.Combine(Path.GetTempPath(), "observables-nats-test", NatsServerVersion);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ".lock");
        for (var i = 0; i < 50; i++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }

        throw new TimeoutException("Timed out waiting for the NATS test-server binary lock.");
    }
    static void WaitForCrossProcessGate()
    {
        CrossProcessServerBinaryGate.WaitOne();
    }

    static async Task<string> EnsureNatsServerPathCoreAsync(CancellationToken cancellationToken)
    {
        // Archive hashes are from the upstream v2.10.28 release SHA256SUMS. The executable hashes
        // are from the exact entries in those verified archives.
        var (platform, extension, executable, archiveSha256, executableSha256) = OperatingSystem.IsWindows()
            ? ("windows-amd64", ".zip", "nats-server.exe",
                "98b8571231b00ebe0445cc7f317f6425c1fbc2edecd455949d37022e04281657",
                "408557b55fd5f632658ab65ae2df3c40d5286d61c4e3216a3eab549dee03b01b")
            : OperatingSystem.IsLinux()
                ? ("linux-amd64", ".tar.gz", "nats-server",
                    "52e3baee2bf9cb4f3d362e293538e9f11ccfdb22ac9fbbd2bcad790e3ba41e3e",
                    "ae0f9b9d3852d1ed24d8751656eb33c2faf2baf08dabd98c4b95fb2d5bb2634f")
                : throw new PlatformNotSupportedException("E2E NATS tests require Windows or Linux CI agents.");
        var root = Path.Combine(Path.GetTempPath(), "observables-nats-test", NatsServerVersion);
        var exePath = Path.Combine(root, executable);
        if (!HasSha256(exePath, executableSha256))
        {
            Directory.CreateDirectory(root);
            var archiveRoot = $"nats-server-{NatsServerVersion}-{platform}";
            var archive = archiveRoot + extension;
            var archivePath = Path.Combine(root, archive);
            if (!HasSha256(archivePath, archiveSha256))
            {
                var downloadUrl = $"https://github.com/nats-io/nats-server/releases/download/{NatsServerVersion}/{archive}";
                using var client = new HttpClient();
                await using var stream = await client.GetStreamAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                await WriteVerifiedFileAsync(stream, archivePath, archiveSha256, cancellationToken).ConfigureAwait(false);
            }

            await ExtractServerAsync(
                archivePath, $"{archiveRoot}/{executable}", exePath, executableSha256, cancellationToken).ConfigureAwait(false);
        }

        EnsureExecutable(exePath);
        return exePath;
    }

    static async Task ExtractServerAsync(
        string archivePath,
        string entryPath,
        string exePath,
        string sha256,
        CancellationToken cancellationToken)
    {
        if (archivePath.EndsWith(".zip", StringComparison.Ordinal))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var entry = zip.GetEntry(entryPath)
                ?? throw new InvalidDataException($"Missing {entryPath} in the NATS server archive.");
            await using var data = entry.Open();
            await WriteVerifiedFileAsync(data, exePath, sha256, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var archive = File.OpenRead(archivePath);
        await using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        await using var tar = new TarReader(gzip);
        while (await tar.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false) is { } entry)
        {
            if (entry.Name == entryPath && entry.DataStream is { } data)
            {
                await WriteVerifiedFileAsync(data, exePath, sha256, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw new InvalidDataException($"Missing {entryPath} in the NATS server archive.");
    }

    internal static bool HasSha256(string path, string expected)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var file = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(file)), expected, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task WriteVerifiedFileAsync(
        Stream source,
        string path,
        string sha256,
        CancellationToken cancellationToken)
    {
        var partialPath = $"{path}.{Guid.NewGuid():N}.partial";
        try
        {
            await using (var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            if (!HasSha256(partialPath, sha256))
            {
                throw new InvalidDataException($"SHA-256 mismatch for {Path.GetFileName(path)}.");
            }

            File.Move(partialPath, path, overwrite: true);
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
