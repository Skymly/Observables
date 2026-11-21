using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Observables.Postgres.Tests.Infrastructure;

/// <summary>
/// B-tier portable PostgreSQL peer for E2E tests (downloads zonky minimal binaries when missing).
/// <para>
/// Binary cache: <c>{TempPath}/observables-postgres-test/{version}/{platform}/</c>
/// holds the verified Maven Central jar. Each fixture extracts its tools to a fresh directory
/// after checking the jar against the pinned platform SHA-256.
/// </para>
/// <para>
/// Test execution: use <see cref="PostgresTestServerCollection"/> with
/// <c>DisableParallelization = true</c> so one peer is shared serially within the assembly
/// (same pattern as Nats). Windows and Linux amd64 only; macOS is not supported.
/// </para>
/// </summary>
public sealed class PostgresTestServer : IAsyncDisposable
{
    /// <summary>Pinned zonky embedded-postgres-binaries version (PostgreSQL 17.4).</summary>
    public const string PostgresBinariesVersion = "17.4.0";

    readonly string dataDirectory;
    readonly string binDirectory;
    readonly int port;
    readonly string connectionString;
    bool stopped;

    PostgresTestServer(string dataDirectory, string binDirectory, int port, string connectionString)
    {
        this.dataDirectory = dataDirectory;
        this.binDirectory = binDirectory;
        this.port = port;
        this.connectionString = connectionString;
    }

    /// <summary>Npgsql connection string for the loopback peer (trust auth, database postgres).</summary>
    public string ConnectionString => connectionString;

    public int Port => port;

    public static async Task<PostgresTestServer> StartAsync(CancellationToken cancellationToken = default)
    {
        var toolsRoot = Directory.CreateTempSubdirectory("observables-postgres-tools-").FullName;
        try
        {
            var binDirectory = await StagePostgresToolsAsync(toolsRoot, cancellationToken).ConfigureAwait(false);
            return await StartPeerAsync(binDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDeleteDirectory(toolsRoot);
            throw;
        }
    }

    static async Task<PostgresTestServer> StartPeerAsync(string binDirectory, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var port = ReserveFreeTcpPort();
            var instanceRoot = Path.Combine(
                Path.GetTempPath(),
                $"observables-postgres-{port}-{Guid.NewGuid():N}");
            var dataDirectory = Path.Combine(instanceRoot, "data");
            Directory.CreateDirectory(instanceRoot);
            var started = false;
            try
            {
                await RunPostgresToolAsync(
                    binDirectory,
                    "initdb",
                    [
                        "-D", dataDirectory,
                        "-U", "postgres",
                        "--auth=trust",
                        "--no-locale",
                        "-E", "UTF8",
                    ],
                    cancellationToken).ConfigureAwait(false);

                var logPath = Path.Combine(instanceRoot, "postgres.log");
                // Do not use pg_ctl -w with redirected stdio — on Windows the waiter can hang
                // after the server is already accepting connections. Port polling is the readiness gate.
                StartPostgresDetached(
                    binDirectory,
                    [
                        "-D", dataDirectory,
                        "-l", logPath,
                        "-o", $"-p {port} -h 127.0.0.1",
                        "start",
                    ]);
                started = true;

                await WaitForPortAsync(port, cancellationToken).ConfigureAwait(false);

                var connectionString =
                    $"Host=127.0.0.1;Port={port};Username=postgres;Database=postgres;Pooling=false;SSL Mode=Disable";
                return new PostgresTestServer(dataDirectory, binDirectory, port, connectionString);
            }
            catch (Exception ex)
            {
                if (started)
                {
                    TryStopPostgres(binDirectory, dataDirectory);
                }

                TryDeleteDirectory(instanceRoot);
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                last = ex;
            }
        }

        throw new InvalidOperationException("Failed to start PostgreSQL test peer.", last);
    }

    static void TryStopPostgres(string binDirectory, string dataDirectory)
    {
        try
        {
            var startInfo = CreatePostgresStartInfo(
                binDirectory,
                OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl",
                ["-D", dataDirectory, "-m", "fast", "-w", "stop"],
                redirect: false);
            using var process = Process.Start(startInfo);
            process?.WaitForExit(30_000);
        }
        catch
        {
            // best-effort rollback of a failed start
        }
    }

    static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of private fixture directories.
        }
    }

    static async Task<string> StagePostgresToolsAsync(string toolsRoot, CancellationToken cancellationToken)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("E2E PostgreSQL tests require an amd64 process.");
        }

        // SHA-256 sidecars of the 17.4.0 jars published under io/zonky/test/postgres on Maven Central.
        var (platform, txzName, sha256) = OperatingSystem.IsWindows()
            ? ("windows-amd64", "postgres-windows-x86_64.txz",
                "629EC928DACC0C86738AD8F798DFFB3E375A16C4B843C7321A4695242CD50523")
            : OperatingSystem.IsLinux()
                ? ("linux-amd64", "postgres-linux-x86_64.txz",
                    "D9D216D3C1C119AD31B8A8DE60B3CF2826516F711A04D3745EF4F1913F21A938")
                : throw new PlatformNotSupportedException(
                    "E2E PostgreSQL tests require Windows or Linux amd64 CI agents.");

        var cacheRoot = Path.Combine(Path.GetTempPath(), "observables-postgres-test", PostgresBinariesVersion, platform);
        Directory.CreateDirectory(cacheRoot);
        var artifactId = $"embedded-postgres-binaries-{platform}";
        var jarName = $"{artifactId}-{PostgresBinariesVersion}.jar";
        var jarPath = Path.Combine(cacheRoot, jarName);
        var url = $"https://repo.maven.apache.org/maven2/io/zonky/test/postgres/{artifactId}/{PostgresBinariesVersion}/{jarName}";
        var txzPath = Path.Combine(toolsRoot, txzName);

        using (var client = new HttpClient())
        await using (var jar = await OpenPinnedArchiveAsync(jarPath, sha256, ct => client.GetStreamAsync(url, ct),
                         cancellationToken).ConfigureAwait(false))
        using (var archive = new ZipArchive(jar, ZipArchiveMode.Read, leaveOpen: true))
        {
            var entry = archive.GetEntry(txzName)
                ?? throw new InvalidDataException($"PostgreSQL archive '{txzName}' is missing from {jarName}.");
            await using var source = entry.Open();
            await using var output = File.Create(txzPath);
            await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        await ExtractTarAsync(txzPath, toolsRoot, cancellationToken).ConfigureAwait(false);
        File.Delete(txzPath);

        var binDirectory = Path.Combine(toolsRoot, "bin");
        foreach (var tool in new[] { "initdb", "pg_ctl", "postgres" })
        {
            var toolName = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
            var path = Path.Combine(binDirectory, toolName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"PostgreSQL tool '{toolName}' is missing from {jarName}.", path);
        }

        EnsureUnixExecutables(binDirectory);
        return binDirectory;
    }

    internal static async Task<FileStream> OpenPinnedArchiveAsync(
        string path, string sha256, Func<CancellationToken, Task<Stream>> download, CancellationToken cancellationToken)
    {
        if (await TryOpenVerifiedArchiveAsync(path, sha256, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var partialPath = $"{path}.{Guid.NewGuid():N}.partial";
        try
        {
            await using (var source = await download(cancellationToken).ConfigureAwait(false))
            await using (var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            var verified = await TryOpenVerifiedArchiveAsync(partialPath, sha256, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException($"SHA-256 mismatch for PostgreSQL archive '{Path.GetFileName(path)}'.");
            try
            {
                // Concurrent fixtures can replace a cache path with identical verified bytes.
                File.Move(partialPath, path, overwrite: true);
                return verified;
            }
            catch
            {
                await verified.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            File.Delete(partialPath);
        }
    }

    static async Task<FileStream?> TryOpenVerifiedArchiveAsync(string path, string sha256, CancellationToken cancellationToken)
    {
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        try
        {
            var actual = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
            if (string.Equals(Convert.ToHexString(actual), sha256, StringComparison.OrdinalIgnoreCase))
            {
                file.Position = 0;
                // Extract from the checked handle even if another fixture replaces the cache path.
                return file;
            }
        }
        catch
        {
            await file.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await file.DisposeAsync().ConfigureAwait(false);
        return null;
    }

    static async Task ExtractTarAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        using var extraction = Process.Start(new ProcessStartInfo
        {
            FileName = "tar",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            ArgumentList = { "-xJf", archivePath, "-C", destination },
        }) ?? throw new InvalidOperationException("Failed to start tar for PostgreSQL extraction.");

        var stderr = extraction.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await extraction.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!extraction.HasExited)
                extraction.Kill(entireProcessTree: true);
            await extraction.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }

        var error = await stderr.ConfigureAwait(false);
        if (extraction.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"tar failed to extract PostgreSQL archive with exit code {extraction.ExitCode}: {error}");
        }
    }

    static void EnsureUnixExecutables(string binDirectory)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        foreach (var name in new[] { "initdb", "pg_ctl", "postgres", "psql" })
        {
            var path = Path.Combine(binDirectory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(
                path,
                mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    static void StartPostgresDetached(string binDirectory, IReadOnlyList<string> arguments)
    {
        var exeName = OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl";
        var startInfo = CreatePostgresStartInfo(binDirectory, exeName, arguments, redirect: false);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start pg_ctl.");
        // Without -w, pg_ctl exits after spawning postgres; do not redirect stdio (Windows hang).
        if (!process.WaitForExit(30_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }

            throw new TimeoutException("pg_ctl start did not exit within 30 seconds.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pg_ctl start exited with code {process.ExitCode}.");
        }
    }

    static async Task RunPostgresToolAsync(
        string binDirectory,
        string toolName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var exeName = OperatingSystem.IsWindows() ? $"{toolName}.exe" : toolName;
        var startInfo = CreatePostgresStartInfo(binDirectory, exeName, arguments, redirect: true);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {toolName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{toolName} exited with code {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
        }
    }

    static ProcessStartInfo CreatePostgresStartInfo(
        string binDirectory,
        string exeName,
        IReadOnlyList<string> arguments,
        bool redirect)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(binDirectory, exeName),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            WorkingDirectory = binDirectory,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Windows builds need sibling DLLs on PATH.
        var pathEnv = startInfo.Environment["PATH"] ?? string.Empty;
        startInfo.Environment["PATH"] = string.IsNullOrEmpty(pathEnv)
            ? binDirectory
            : binDirectory + Path.PathSeparator + pathEnv;
        return startInfo;
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
        for (var i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"PostgreSQL did not listen on port {port}.");
    }

    public async ValueTask DisposeAsync()
    {
        if (stopped)
        {
            return;
        }

        stopped = true;
        Exception? stopError = null;
        try
        {
            var startInfo = CreatePostgresStartInfo(
                binDirectory,
                OperatingSystem.IsWindows() ? "pg_ctl.exe" : "pg_ctl",
                ["-D", dataDirectory, "-m", "fast", "-w", "stop"],
                redirect: false);
            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    stopError = new InvalidOperationException(
                        $"pg_ctl stop exited with code {process.ExitCode}.");
                }
            }
        }
        catch (Exception ex)
        {
            stopError ??= ex;
        }

        var instanceRoot = Path.GetDirectoryName(dataDirectory);
        if (instanceRoot is not null)
        {
            TryDeleteDirectory(instanceRoot);
        }

        TryDeleteDirectory(Path.GetDirectoryName(binDirectory)!);

        if (stopError is InvalidOperationException)
        {
            throw stopError;
        }
    }
}
