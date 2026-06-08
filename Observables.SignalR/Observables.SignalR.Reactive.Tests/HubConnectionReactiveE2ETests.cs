using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR;
using Observables.SignalR.Reactive.Tests.Contracts;
using Observables.SignalR.Tests.Infrastructure;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.SignalR.Reactive.Tests;

[Collection(nameof(SignalRTestServerCollection))]
public sealed class HubConnectionReactiveE2ETests(SignalRTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task HubInvoke_Add_returns_sum()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        var sum = await hub.Add(2, 3).Timeout(DefaultTimeout).FirstAsync().ToTask();

        Assert.Equal(5, sum);
    }

    [Fact]
    public async Task HubSend_EchoSend_completes()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        await hub.EchoSend("ping").Timeout(DefaultTimeout).FirstAsync().ToTask();
    }

    [Fact]
    public async Task HubStream_Counter_emits_sequence()
    {
        await using var connection = await ConnectStreamingAsync();
        var hub = HubService.For<IE2EHub>(connection);

        var values = await hub.Counter(3).Take(3).ToList().ToTask();

        Assert.Equal([0, 1, 2], values);
    }

    [Fact]
    public async Task HubOn_Notify_receives_server_push()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Notify.Timeout(DefaultTimeout).FirstAsync().ToTask();
        await connection.InvokeAsync("PushNotify", "hello", cts.Token);
        var message = await receive;

        Assert.Equal("hello", message);
    }

    async Task<HubConnection> ConnectAsync() => await StartAsync(fixture.Server.CreateConnection());

    async Task<HubConnection> ConnectStreamingAsync() =>
        await StartAsync(fixture.Server.CreateStreamingConnection());

    static async Task<HubConnection> StartAsync(HubConnection connection)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await connection.StartAsync(cts.Token);
        return connection;
    }
}
