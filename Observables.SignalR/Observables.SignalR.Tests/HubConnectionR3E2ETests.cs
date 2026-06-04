using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR;
using Observables.SignalR.Tests.Contracts;
using Observables.SignalR.Tests.Infrastructure;
using R3;

namespace Observables.SignalR.Tests;

[Collection(nameof(SignalRTestServerCollection))]
public sealed class HubConnectionR3E2ETests(SignalRTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task HubInvoke_Add_returns_sum()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var sum = await hub.Add(2, 3).FirstAsync(cts.Token);

        Assert.Equal(5, sum);
    }

    [Fact]
    public async Task HubInvoke_Add_direct_invoke_matches_generated_proxy()
    {
        await using var connection = await ConnectAsync();

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var direct = await connection.InvokeAsync<int>("Add", 2, 3, cts.Token);
        var viaProxy = await HubService.For<IE2EHub>(connection).Add(2, 3).FirstAsync(cts.Token);

        Assert.Equal(5, direct);
        Assert.Equal(direct, viaProxy);
    }

    [Fact]
    public async Task HubSend_EchoSend_completes()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        await hub.EchoSend("ping").FirstAsync(cts.Token);
    }

    [Fact]
    public async Task HubStream_Counter_emits_sequence()
    {
        await using var connection = await ConnectStreamingAsync();
        var hub = HubService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var values = new List<int>();
        await foreach (var value in hub.Counter(3).ToAsyncEnumerable(cts.Token))
        {
            values.Add(value);
        }

        Assert.Equal([0, 1, 2], values);
    }

    [Fact]
    public async Task HubOn_Notify_receives_server_push()
    {
        await using var connection = await ConnectAsync();
        var hub = HubService.For<IE2EHub>(connection);

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var receive = hub.Notify.FirstAsync(cts.Token);
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
