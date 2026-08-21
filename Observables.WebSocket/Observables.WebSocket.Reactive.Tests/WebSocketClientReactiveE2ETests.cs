using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using Observables.WebSocket;
using Observables.WebSocket.Reactive;
using Observables.WebSocket.Reactive.Tests.Contracts;
using Observables.WebSocket.Tests.Infrastructure;

namespace Observables.WebSocket.Reactive.Tests;

[Collection(nameof(WebSocketTestServerCollection))]
public sealed class WebSocketClientReactiveE2ETests(WebSocketTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Connect_and_Close_succeeds()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();
        Assert.Equal(WebSocketState.Open, socket.State);

        await hub.Close(cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();
        Assert.NotEqual(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task SendText_echoes_string_content()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        const string expected = "hello-reactive-websocket";
        var receiveTask = hub.EchoText.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendText(expected, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var received = await receiveTask;
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task SendBytes_echoes_binary_content()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var expected = new byte[] { 0x01, 0x02, 0x03, 0xAB, 0xCD };
        var receiveTask = hub.EchoBytes.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendBytes(expected, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var received = await receiveTask;
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task SendText_large_message_assembles_correctly()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        // 20 KB text — exceeds typical 4096-byte single-receive buffer
        var expected = new string('y', 20 * 1024);
        var receiveTask = hub.EchoText.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendText(expected, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var received = await receiveTask;
        Assert.Equal(expected.Length, received.Length);
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task FromReceive_dispose_cancels_the_pump_without_completing()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveWebSocketAdapter.FromReceive<string>(socket)
            .Subscribe(
                value => ready.TrySetResult(value),
                _ => Interlocked.Exchange(ref errored, 1),
                () => Interlocked.Exchange(ref completed, 1));

        var payload = Encoding.UTF8.GetBytes("ready");
        await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cts.Token);

        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));

        subscription.Dispose();
        await Task.Delay(200, cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }
}
