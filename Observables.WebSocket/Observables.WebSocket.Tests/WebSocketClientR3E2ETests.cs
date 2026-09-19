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
    public async Task Named_receive_unwraps_the_envelope_a_named_send_wrote()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        var receiveTask = hub.Chats.FirstAsync(cts.Token);
        await hub.SendChat("hello", cts.Token).FirstAsync(cts.Token);

        Assert.Equal("hello", await receiveTask);
    }

    [Fact]
    public async Task Named_receive_ignores_an_envelope_addressed_elsewhere()
    {
        using var socket = new ClientWebSocket();
        var hub = WebSocketService.For<IE2EHub>(socket);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.Connect(fixture.Server.Uri, cts.Token).FirstAsync(cts.Token);

        var chats = new List<string>();
        using var subscription = hub.Chats.Subscribe(value => chats.Add(value));

        // "ping" is echoed back as {"type":"ping"}; the raw stream sees it, the chat stream must not.
        var rawTask = hub.EchoText.FirstAsync(cts.Token);
        await hub.Ping(cts.Token).FirstAsync(cts.Token);
        Assert.Equal("{\"type\":\"ping\"}", await rawTask);

        // Now a chat envelope, which must arrive — proving the stream was live all along.
        var chatTask = hub.Chats.FirstAsync(cts.Token);
        await hub.SendChat("hello", cts.Token).FirstAsync(cts.Token);
        Assert.Equal("hello", await chatTask);

        Assert.Equal(["hello"], chats);
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
    public async Task Two_subscribers_see_the_same_message()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var stream = WebSocketObservable.FromReceive<string>(socket);
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
    public async Task A_subscriber_arriving_after_the_last_one_left_still_receives()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var stream = WebSocketObservable.FromReceive<string>(socket);

        var early = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var earlySubscription = stream.Subscribe(value => early.TrySetResult(value));
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("first")),
            WebSocketMessageType.Text,
            true,
            cts.Token);
        Assert.Equal("first", await early.Task.WaitAsync(cts.Token));
        earlySubscription.Dispose();

        var late = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lateSubscription = stream.Subscribe(value => late.TrySetResult(value));
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("second")),
            WebSocketMessageType.Text,
            true,
            cts.Token);

        Assert.Equal("second", await late.Task.WaitAsync(cts.Token));
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

    [Fact]
    public async Task FromReceive_subscribe_before_connect_still_receives()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = WebSocketObservable.FromReceive<string>(socket)
            .Subscribe(value => received.TrySetResult(value));

        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("after-connect")),
            WebSocketMessageType.Text,
            true,
            cts.Token);

        Assert.Equal("after-connect", await received.Task.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task FromReceive_abort_completes_with_failure()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var observer = new RecordingObserver<string>();
        using var subscription = WebSocketObservable.FromReceive<string>(socket).Subscribe(observer);
        socket.Abort();

        var result = await observer.Completed.WaitAsync(cts.Token);
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task FromReceiveNamed_matching_bad_payload_is_observable()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var observer = new RecordingObserver<int>();
        using var subscription = WebSocketObservable.FromReceiveNamed<int>(socket, "tick").Subscribe(observer);

        var frame = Encoding.UTF8.GetBytes("""{"type":"tick","payload":"bad"}""");
        await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Text, true, cts.Token);

        var error = await observer.Error.WaitAsync(cts.Token);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task First_subscriber_still_receives_after_a_second_attaches()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var stream = WebSocketObservable.FromReceive<string>(socket);
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstSubscription = stream.Subscribe(value => first.TrySetResult(value));
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var secondSubscription = stream.Subscribe(value => second.TrySetResult(value));

        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("both")),
            WebSocketMessageType.Text,
            true,
            cts.Token);

        Assert.Equal("both", await first.Task.WaitAsync(cts.Token));
        Assert.Equal("both", await second.Task.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task FromReceive_non_positive_max_fails_once()
    {
        using var socket = new ClientWebSocket();
        var observer = new RecordingObserver<string>();
        using var subscription = WebSocketObservable.FromReceive<string>(socket, maxMessageBytes: 0)
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.IsType<ArgumentOutOfRangeException>(result.Exception);
    }

    [Fact]
    public async Task FromReceive_oversize_then_resubscribe_receives_next_message()
    {
        using var socket = new ClientWebSocket();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await socket.ConnectAsync(fixture.Server.Uri, cts.Token);

        var observer = new RecordingObserver<string>();
        var subscription = WebSocketObservable.FromReceive<string>(socket, maxMessageBytes: 8).Subscribe(observer);
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(new string('x', 64))),
            WebSocketMessageType.Text,
            true,
            cts.Token);
        var failed = await observer.Completed.WaitAsync(cts.Token);
        Assert.True(failed.IsFailure);
        subscription.Dispose();

        var next = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var nextSubscription = WebSocketObservable.FromReceive<string>(socket).Subscribe(value => next.TrySetResult(value));
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("ok")),
            WebSocketMessageType.Text,
            true,
            cts.Token);
        Assert.Equal("ok", await next.Task.WaitAsync(cts.Token));
    }
    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource<Exception> _error = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Result> Completed => _completed.Task;

        public Task<Exception> Error => _error.Task;

        protected override void OnNextCore(T value)
        {
        }

        protected override void OnErrorResumeCore(Exception error) => _error.TrySetResult(error);

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }
}
