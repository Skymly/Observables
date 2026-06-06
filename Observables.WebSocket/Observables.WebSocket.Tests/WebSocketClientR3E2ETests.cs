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
}
