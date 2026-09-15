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
    public async Task Named_send_wraps_the_payload_in_the_envelope()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var receiveTask = hub.EchoText.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendChat("hello", cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal("{\"type\":\"chat\",\"payload\":\"hello\"}", await receiveTask);
    }

    [Fact]
    public async Task Named_receive_unwraps_the_envelope_a_named_send_wrote()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var receiveTask = hub.Chats.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendChat("hello", cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal("hello", await receiveTask);
    }

    [Fact]
    public async Task Named_receive_ignores_an_envelope_addressed_elsewhere()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();

        var chats = new List<string>();
        using var subscription = hub.Chats.Subscribe(value => chats.Add(value));

        // "ping" is echoed back as {"type":"ping"}; the raw stream sees it, the chat stream must not.
        var rawTask = hub.EchoText.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.Ping(cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();
        Assert.Equal("{\"type\":\"ping\"}", await rawTask);

        // Now a chat envelope, which must arrive — proving the stream was live all along.
        var chatTask = hub.Chats.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await hub.SendChat("hello", cts.Token).Timeout(DefaultTimeout).FirstAsync().ToTask();
        Assert.Equal("hello", await chatTask);

        Assert.Equal(["hello"], chats);
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
    public async Task FromReceive_dispose_detaches_without_completing_and_leaves_the_socket_usable()
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

        // Cancelling a pending ReceiveAsync aborts the whole socket, so detaching must not cancel one.
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("still-open")),
            WebSocketMessageType.Text,
            true,
            cts.Token);
        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task Two_subscribers_see_the_same_message()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var stream = SystemReactiveWebSocketAdapter.FromReceive<string>(socket);
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var firstSubscription = stream.Subscribe(value => first.TrySetResult(value));
        using var secondSubscription = stream.Subscribe(value => second.TrySetResult(value));

        var payload = Encoding.UTF8.GetBytes("shared");
        await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cts.Token);

        Assert.Equal("shared", await first.Task.WaitAsync(cts.Token));
        Assert.Equal("shared", await second.Task.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task FromReceive_message_over_cap_completes_with_error()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = SystemReactiveWebSocketAdapter.FromReceive<string>(socket, maxMessageBytes: 16)
            .Subscribe(
                _ => { },
                ex => error.TrySetResult(ex),
                () => { });

        var payload = Encoding.UTF8.GetBytes(new string('x', 64));
        await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cts.Token);

        var ex = await error.Task.WaitAsync(cts.Token);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Contains("maximum size", ex.Message, StringComparison.Ordinal);
    }
}
