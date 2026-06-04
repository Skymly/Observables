using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR.Tests.Infrastructure;

public static class HubConnectionAsyncDisposableExtensions
{
    public static async ValueTask DisposeAsync(this HubConnection connection)
    {
        if (connection.State == HubConnectionState.Disconnected)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await connection.StopAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
