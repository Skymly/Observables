using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Observables.SignalR;
using Observables.SignalR.Reactive.Tests.Contracts;
using Observables.SignalR.Tests.Infrastructure;
using System.Net;
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
    public async Task FromInvoke_dispose_cancels_a_pending_invocation()
    {
        await using var connection = (TrackedHubConnection)await StartAsync(
            fixture.Server.CreateConnection(builder => builder.Services.AddSingleton<HubConnection, TrackedHubConnection>()));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = connection.On("InvokeStarted", () => started.TrySetResult());
        using var subscription = SystemReactiveSignalRAdapter.FromInvoke<int>(connection, "HoldInvoke", TestContext.Current.CancellationToken)
            .Subscribe(_ => { });

        await started.Task.WaitAsync(timeout.Token);
        Assert.False(connection.InvocationEnded.IsCompleted);
        subscription.Dispose();

        Assert.IsAssignableFrom<OperationCanceledException>(await connection.InvocationEnded.WaitAsync(timeout.Token));
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

    sealed class TrackedHubConnection(
        IConnectionFactory connectionFactory,
        IHubProtocol protocol,
        EndPoint endPoint,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : HubConnection(connectionFactory, protocol, endPoint, serviceProvider, loggerFactory)
    {
        // Rx detaches the observer on disposal. Observe the real invocation result at the connection.
        readonly TaskCompletionSource<Exception?> invocationEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Exception?> InvocationEnded => invocationEnded.Task;

        public override async Task<object?> InvokeCoreAsync(
            string methodName, Type returnType, object?[] args, CancellationToken cancellationToken = default)
        {
            try
            {
                object? result = await base.InvokeCoreAsync(methodName, returnType, args, cancellationToken);
                invocationEnded.TrySetResult(null);
                return result;
            }
            catch (Exception error)
            {
                invocationEnded.TrySetResult(error);
                throw;
            }
        }
    }
}
