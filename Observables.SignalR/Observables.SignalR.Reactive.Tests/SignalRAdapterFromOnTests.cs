using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR.Reactive;
using Observables.SignalR.Tests.Infrastructure;

namespace Observables.SignalR.Reactive.Tests;

public sealed class SignalRAdapterFromOnTests
{
    [Fact]
    public async Task FromOn_second_subscriber_does_not_register_another_handler()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:1/hub")
            .Build();

        using var first = SystemReactiveSignalRAdapter.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        using var second = SystemReactiveSignalRAdapter.FromOn<string>(connection, "Notify").Subscribe(_ => { });

        Assert.Equal(1, HubConnectionOnHandlers.Count(connection, "Notify"));
    }

    [Fact]
    public async Task FromOn_last_unsubscribe_removes_handler()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:1/hub")
            .Build();

        var first = SystemReactiveSignalRAdapter.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        var second = SystemReactiveSignalRAdapter.FromOn<string>(connection, "Notify").Subscribe(_ => { });
        Assert.Equal(1, HubConnectionOnHandlers.Count(connection, "Notify"));

        first.Dispose();
        Assert.Equal(1, HubConnectionOnHandlers.Count(connection, "Notify"));

        second.Dispose();
        Assert.Equal(0, HubConnectionOnHandlers.Count(connection, "Notify"));
    }
}
