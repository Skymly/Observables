using Microsoft.AspNetCore.SignalR;
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


    [Fact]
    public async Task FromInvoke_dispose_cancels_without_ObjectDisposedException()
    {
        await using var connection = await ConnectAsync();
        var error = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveSignalRAdapter.FromInvoke<int>(connection, "HoldInvoke", TestContext.Current.CancellationToken)
            .Subscribe(
                _ => { },
                ex => error.TrySetResult(ex),
                () => { });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        await Task.Delay(50, timeout.Token);
        subscription.Dispose();

        var ex = await error.Task.WaitAsync(timeout.Token);
        Assert.True(ex is OperationCanceledException or HubException, ex.GetType().FullName);
        Assert.IsNotType<ObjectDisposedException>(ex);
    }

    [Fact]
    public async Task FromStream_dispose_cancels_the_pump_without_completing()
    {
        await using var connection = await ConnectStreamingAsync();
        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveSignalRAdapter.FromStream<int>(connection, "Hold", TestContext.Current.CancellationToken)
            .Subscribe(
                value => ready.TrySetResult(value),
                _ => Interlocked.Exchange(ref errored, 1),
                () => Interlocked.Exchange(ref completed, 1));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        Assert.Equal(0, await ready.Task.WaitAsync(timeout.Token));

        subscription.Dispose();
        await Task.Delay(200, timeout.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
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
