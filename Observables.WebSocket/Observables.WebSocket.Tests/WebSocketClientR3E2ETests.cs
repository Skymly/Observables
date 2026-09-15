using System.Net.WebSockets;
using System.Text;
using Observables.WebSocket;
using Observables.WebSocket.Tests.Contracts;
using Observables.WebSocket.Tests.Infrastructure;
using R3;

namespace Observables.WebSocket.Tests;

[Collection(nameof(WebSocketTestServerCollection))]
public sealed class WebSocketClientR3E2ETests(WebSocketTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Connect_and_Close_succeeds()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);
        Assert.Equal(WebSocketState.Open, socket.State);

        await hub.Close(cts.Token).FirstAsync(cts.Token);
        Assert.NotEqual(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task SendText_echoes_string_content()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        const string expected = "hello-websocket";
        var receiveTask = hub.EchoText.FirstAsync(cts.Token);
        await hub.SendText(expected, cts.Token).FirstAsync(cts.Token);

        var received = await receiveTask;
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task Named_send_without_a_payload_puts_the_name_on_the_wire()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        var receiveTask = hub.EchoText.FirstAsync(cts.Token);
        await hub.Ping(cts.Token).FirstAsync(cts.Token);

        Assert.Equal("{\"type\":\"ping\"}", await receiveTask);
    }

    [Fact]
    public async Task Named_send_wraps_the_payload_in_the_envelope()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        var receiveTask = hub.EchoText.FirstAsync(cts.Token);
        await hub.SendChat("hello", cts.Token).FirstAsync(cts.Token);

        Assert.Equal("{\"type\":\"chat\",\"payload\":\"hello\"}", await receiveTask);
    }

    [Fact]
    public async Task SendBytes_echoes_binary_content()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        var expected = new byte[] { 0x01, 0x02, 0x03, 0xAB, 0xCD };
        var receiveTask = hub.EchoBytes.FirstAsync(cts.Token);
        await hub.SendBytes(expected, cts.Token).FirstAsync(cts.Token);

        var received = await receiveTask;
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task SendText_large_message_assembles_correctly()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        // 20 KB text — exceeds typical 4096-byte single-receive buffer
        var expected = new string('x', 20 * 1024);
        var receiveTask = hub.EchoText.FirstAsync(cts.Token);
        await hub.SendText(expected, cts.Token).FirstAsync(cts.Token);

        var received = await receiveTask;
        Assert.Equal(expected.Length, received.Length);
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task FromReceive_message_over_cap_completes_with_error()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var observer = new RecordingObserver<string>();
        using var subscription = WebSocketObservable.FromReceive<string>(socket, maxMessageBytes: 16).Subscribe(observer);

        var payload = Encoding.UTF8.GetBytes(new string('x', 64));
        await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cts.Token);

        var result = await observer.Completed.WaitAsync(cts.Token);
        Assert.True(result.IsFailure);
        Assert.IsAssignableFrom<InvalidOperationException>(result.Exception);
        Assert.Contains("maximum size", result.Exception.Message, StringComparison.Ordinal);
    }

    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Result> Completed => _completed.Task;

        protected override void OnNextCore(T value)
        {
        }

        protected override void OnErrorResumeCore(Exception error)
        {
        }

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }
}
