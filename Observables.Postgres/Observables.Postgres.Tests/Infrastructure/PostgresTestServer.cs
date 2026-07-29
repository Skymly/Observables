using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace Observables.Postgres.Tests.Infrastructure;

/// <summary>
/// B-tier portable PostgreSQL peer for E2E tests (downloads zonky minimal binaries when missing).
/// <para>
/// Binary cache: <c>{TempPath}/observables-postgres-test/{version}/{platform}/</c>
/// (Maven Central jar → nested <c>.txz</c> extract). Download/extract is gated by a process-wide
/// <see cref="SemaphoreSlim"/> so parallel assemblies do not corrupt the cache.
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

    static readonly SemaphoreSlim ServerBinaryGate = new(1, 1);

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
        var port = ReserveFreeTcpPort();
        var binDirectory = await EnsurePostgresBinDirectoryAsync(cancellationToken).ConfigureAwait(false);
        var instanceRoot = Path.Combine(
            Path.GetTempPath(),
            $"observables-postgres-{port}-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(instanceRoot, "data");
        Directory.CreateDirectory(instanceRoot);

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

        await WaitForPortAsync(port, cancellationToken).ConfigureAwait(false);

        var connectionString =
            $"Host=127.0.0.1;Port={port};Username=postgres;Database=postgres;Pooling=false;SSL Mode=Disable";
        return new PostgresTestServer(dataDirectory, binDirectory, port, connectionString);
    }

    static async Task<string> EnsurePostgresBinDirectoryAsync(CancellationToken cancellationToken)
    {
        await ServerBinaryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnsurePostgresBinDirectoryCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ServerBinaryGate.Release();
        }
    }

    static async Task<string> EnsurePostgresBinDirectoryCoreAsync(CancellationToken cancellationToken)
    {
        var platform = OperatingSystem.IsWindows()
            ? "windows-amd64"
            : OperatingSystem.IsLinux()
                ? "linux-amd64"
                : throw new PlatformNotSupportedException(
                    "E2E PostgreSQL tests require Windows or Linux amd64 CI agents.");

        var root = Path.Combine(
            Path.GetTempPath(),
            "observables-postgres-test",
            PostgresBinariesVersion,
            platform);
        var binDirectory = Path.Combine(root, "bin");
        var initdbName = OperatingSystem.IsWindows() ? "initdb.exe" : "initdb";
        var initdbPath = Path.Combine(binDirectory, initdbName);
        if (File.Exists(initdbPath))
        {
            EnsureUnixExecutables(binDirectory);
            return binDirectory;
        }

        Directory.CreateDirectory(root);
        var artifactId = $"embedded-postgres-binaries-{platform}";
        var jarName = $"{artifactId}-{PostgresBinariesVersion}.jar";
        var jarPath = Path.Combine(root, jarName);
        if (!File.Exists(jarPath))
        {
            var downloadUrl =
                $"https://repo1.maven.org/maven2/io/zonky/test/postgres/{artifactId}/{PostgresBinariesVersion}/{jarName}";
            using var client = new HttpClient();
            await using var stream = await client
                .GetStreamAsync(downloadUrl, cancellationToken)
                .ConfigureAwait(false);
            await using var file = File.Create(jarPath);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var txzName = OperatingSystem.IsWindows()
            ? "postgres-windows-x86_64.txz"
            : "postgres-linux-x86_64.txz";
        var txzPath = Path.Combine(root, txzName);
        if (!File.Exists(txzPath))
        {
            ZipFile.ExtractToDirectory(jarPath, root, overwriteFiles: true);
        }

        if (!File.Exists(txzPath))
        {
            throw new FileNotFoundException(
                $"PostgreSQL archive '{txzName}' not found after extracting zonky jar.",
                txzPath);
        }

        using (var extraction = Process.Start(
                   new ProcessStartInfo
                   {
                       FileName = "tar",
                       UseShellExecute = false,
                       CreateNoWindow = true,
                       RedirectStandardOutput = true,
                       RedirectStandardError = true,
                       ArgumentList =
                       {
                           "-xJf",
                           txzPath,
                           "-C",
                           root,
                       },
                   }) ?? throw new InvalidOperationException("Failed to start tar for PostgreSQL extraction."))
        {
            await extraction.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (extraction.ExitCode != 0)
            {
                var stderr = await extraction.StandardError.ReadToEndAsync(cancellationToken)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"tar failed to extract PostgreSQL archive with exit code {extraction.ExitCode}: {stderr}");
            }
        }

        if (!File.Exists(initdbPath))
        {
            throw new FileNotFoundException(
                "PostgreSQL initdb executable not found after extract.",
                initdbPath);
        }

        EnsureUnixExecutables(binDirectory);
        return binDirectory;
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
            }
        }
        catch
        {
            // ignored — best-effort shutdown
        }

        var instanceRoot = Path.GetDirectoryName(dataDirectory);
        if (instanceRoot is not null)
        {
            try
            {
                Directory.Delete(instanceRoot, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }
}
