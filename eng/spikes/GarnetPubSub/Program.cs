using System.Net;
using System.Net.Sockets;
using Garnet;
using StackExchange.Redis;

// Issue #170 — Garnet Pub/Sub capability spike (SUBSCRIBE / PSUBSCRIBE / PUBLISH).
// Seam: StackExchange.Redis ISubscriber against in-process Microsoft.Garnet GarnetServer.
// No Observables.Redis product API.

var workDir = Path.Combine(Path.GetTempPath(), "observables-garnet-pubsub-spike", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workDir);

var port = ReserveFreeTcpPort();
var results = new List<(string Family, bool Pass, string Detail)>();

Console.WriteLine($"Garnet Pub/Sub spike (#170)");
Console.WriteLine($"  workDir = {workDir}");
Console.WriteLine($"  port    = {port}");
Console.WriteLine();

GarnetServer? server = null;
try
{
    server = new GarnetServer(
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
    Console.WriteLine("GarnetServer.Start() OK");
    Console.WriteLine();

    var config = ConfigurationOptions.Parse($"127.0.0.1:{port}");
    config.AbortOnConnectFail = true;
    config.ConnectTimeout = 5000;
    config.SyncTimeout = 5000;
    config.AsyncTimeout = 5000;
    config.AllowAdmin = false;

    await using var mux = await ConnectionMultiplexer.ConnectAsync(config).ConfigureAwait(false);
    var subscriber = mux.GetSubscriber();

    results.Add(await ProbeExactSubscribeAsync(subscriber).ConfigureAwait(false));
    results.Add(await ProbePatternSubscribeAsync(subscriber).ConfigureAwait(false));
    results.Add(await ProbePublishFanoutAsync(subscriber).ConfigureAwait(false));
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex);
    results.Add(("bootstrap", false, ex.Message));
}
finally
{
    try
    {
        server?.Dispose();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Dispose warning: {ex.Message}");
    }

    try
    {
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }
    }
    catch
    {
        // best-effort cleanup
    }
}

Console.WriteLine();
Console.WriteLine("Results");
Console.WriteLine("-------");
foreach (var (family, pass, detail) in results)
{
    Console.WriteLine($"{(pass ? "PASS" : "FAIL"),-4}  {family,-28}  {detail}");
}

var allPass = results.Count > 0 && results.TrueForAll(static r => r.Pass);
Console.WriteLine();
Console.WriteLine(allPass
    ? "DECISION: Garnet locked for Observables.Redis E2E (classic SUBSCRIBE / PSUBSCRIBE / PUBLISH)."
    : "DECISION: Garnet NOT locked — use a documented fallback (portable redis-server / other) for E2E.");
Console.WriteLine(allPass ? "OVERALL: PASS" : "OVERALL: FAIL");

return allPass ? 0 : 1;

static async Task<(string Family, bool Pass, string Detail)> ProbeExactSubscribeAsync(ISubscriber subscriber)
{
    const string family = "SUBSCRIBE (exact)";
    var channel = RedisChannel.Literal($"obs-spike-exact-{Guid.NewGuid():N}");
    const string payload = "exact-hello";
    var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    try
    {
        await subscriber.SubscribeAsync(channel, (_, value) =>
        {
            received.TrySetResult(value.ToString());
        }).ConfigureAwait(false);

        // Give the subscription handshake a moment before publish.
        await Task.Delay(100).ConfigureAwait(false);

        var receivers = await subscriber.PublishAsync(channel, payload).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await received.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);

        if (!string.Equals(message, payload, StringComparison.Ordinal))
        {
            return (family, false, $"payload mismatch: got '{message}', receivers={receivers}");
        }

        return (family, true, $"payload OK, Publish receivers={receivers}");
    }
    catch (Exception ex)
    {
        return (family, false, $"{ex.GetType().Name}: {ex.Message}");
    }
}

static async Task<(string Family, bool Pass, string Detail)> ProbePatternSubscribeAsync(ISubscriber subscriber)
{
    const string family = "PSUBSCRIBE (pattern)";
    var token = Guid.NewGuid().ToString("N")[..8];
    var pattern = RedisChannel.Pattern($"obs-spike-{token}:*");
    var concrete = RedisChannel.Literal($"obs-spike-{token}:news");
    const string payload = "pattern-hello";
    var received = new TaskCompletionSource<(string Channel, string Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);

    try
    {
        await subscriber.SubscribeAsync(pattern, (ch, value) =>
        {
            received.TrySetResult((ch.ToString(), value.ToString()));
        }).ConfigureAwait(false);

        await Task.Delay(100).ConfigureAwait(false);

        var receivers = await subscriber.PublishAsync(concrete, payload).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var (channel, message) = await received.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await subscriber.UnsubscribeAsync(pattern).ConfigureAwait(false);

        if (!string.Equals(message, payload, StringComparison.Ordinal))
        {
            return (family, false, $"payload mismatch: got '{message}' on '{channel}', receivers={receivers}");
        }

        if (!string.Equals(channel, concrete.ToString(), StringComparison.Ordinal))
        {
            return (family, false, $"channel mismatch: got '{channel}', expected '{concrete}', receivers={receivers}");
        }

        return (family, true, $"matched channel '{channel}', Publish receivers={receivers}");
    }
    catch (Exception ex)
    {
        return (family, false, $"{ex.GetType().Name}: {ex.Message}");
    }
}

static async Task<(string Family, bool Pass, string Detail)> ProbePublishFanoutAsync(ISubscriber subscriber)
{
    const string family = "PUBLISH (fan-out)";
    var channel = RedisChannel.Literal($"obs-spike-pub-{Guid.NewGuid():N}");
    const string payload = "publish-hello";
    var a = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var b = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    try
    {
        await subscriber.SubscribeAsync(channel, (_, value) => a.TrySetResult(value.ToString())).ConfigureAwait(false);
        // Second subscription on the same multiplexer still exercises PUBLISH delivery count.
        await subscriber.SubscribeAsync(channel, (_, value) => b.TrySetResult(value.ToString())).ConfigureAwait(false);

        await Task.Delay(100).ConfigureAwait(false);

        var receivers = await subscriber.PublishAsync(channel, payload).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var msgA = await a.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        var msgB = await b.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        await subscriber.UnsubscribeAsync(channel).ConfigureAwait(false);

        if (!string.Equals(msgA, payload, StringComparison.Ordinal) || !string.Equals(msgB, payload, StringComparison.Ordinal))
        {
            return (family, false, $"payload mismatch a='{msgA}' b='{msgB}', receivers={receivers}");
        }

        return (family, true, $"both handlers received payload, Publish receivers={receivers}");
    }
    catch (Exception ex)
    {
        return (family, false, $"{ex.GetType().Name}: {ex.Message}");
    }
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
