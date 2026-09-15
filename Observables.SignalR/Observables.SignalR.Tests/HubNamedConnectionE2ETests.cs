using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR;
using Observables.SignalR.Tests.Contracts;
using Observables.SignalR.Tests.Infrastructure;
using R3;

namespace Observables.SignalR.Tests;

[Collection(nameof(SignalRTestServerCollection))]
public sealed class HubNamedConnectionE2ETests(SignalRTestServerFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Name_on_the_attribute_resolves_the_registered_connection()
    {
        await using var connection = await ConnectAsync();
        HubService.RegisterConnection("e2e-named", connection);

        var hub = HubService.For<IE2ENamedHub>();

        using var cts = new CancellationTokenSource(DefaultTimeout);
        Assert.Equal(5, await hub.Add(2, 3).FirstAsync(cts.Token));
    }

    [Fact]
    public async Task Resolving_a_name_with_no_registered_connection_says_which_name_is_missing()
    {
        await using var connection = await ConnectAsync();
        HubService.RegisterConnection("e2e-named", connection);

        // The proxy name comes from the attribute, so an unregistered name can only be reached by
        // asking for an interface whose name was never registered.
        var error = Assert.Throws<InvalidOperationException>(
            static () => HubService.For(typeof(IE2EUnregisteredHub)));

        Assert.Contains("e2e-unregistered", error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(HubService.RegisterConnection), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_an_unnamed_interface_points_at_the_connection_overload()
    {
        var error = Assert.Throws<InvalidOperationException>(static () => HubService.For(typeof(IE2EHub)));

        Assert.Contains("[Hub(", error.Message, StringComparison.Ordinal);
        Assert.Contains("For<T>(connection)", error.Message, StringComparison.Ordinal);
    }

    async Task<HubConnection> ConnectAsync()
    {
        var connection = fixture.Server.CreateConnection();
        using var cts = new CancellationTokenSource(DefaultTimeout);
        await connection.StartAsync(cts.Token);
        return connection;
    }
}
