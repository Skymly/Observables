using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Observables.Redis.Tests.Infrastructure;

public sealed class RedisTestServerReadinessTests
{
    [Fact]
    public async Task WaitUntilTcpPortAccepts_succeeds_after_delayed_listen()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        var listenTask = Task.Run(
            async () =>
            {
                await Task.Delay(200, cancellation);
                listener.Start();
            },
            cancellation);

        try
        {
            await RedisTestServer.WaitUntilTcpPortAcceptsAsync(
                port,
                cancellation,
                attempts: 40,
                delayMilliseconds: 25);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellation);
        }
        finally
        {
            listener.Stop();
            await listenTask.WaitAsync(cancellation);
        }
    }

    [Fact]
    public async Task WaitUntilTcpPortAccepts_times_out_when_port_never_listens()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        await Assert.ThrowsAsync<TimeoutException>(
            () => RedisTestServer.WaitUntilTcpPortAcceptsAsync(
                port,
                cancellation,
                attempts: 3,
                delayMilliseconds: 10));
    }

    [Fact]
    public async Task WaitUntilTcpPortAccepts_pauses_between_immediate_refusals()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var started = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(
            () => RedisTestServer.WaitUntilTcpPortAcceptsAsync(
                port,
                cancellation,
                attempts: 4,
                delayMilliseconds: 50));

        Assert.True(
            started.ElapsedMilliseconds >= 100,
            $"expected at least 100ms of retry backoff, elapsed {started.ElapsedMilliseconds}ms");
    }
}
