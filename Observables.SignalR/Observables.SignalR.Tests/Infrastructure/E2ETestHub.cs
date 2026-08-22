using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace Observables.SignalR.Tests.Infrastructure;

/// <summary>In-process SignalR hub backing <see cref="Contracts.IE2EHub"/> client contract.</summary>
public sealed class E2ETestHub : Hub
{
    public Task<int> Add(int a, int b) => Task.FromResult(a + b);

    public Task EchoSend(string text) => Task.CompletedTask;

    public async IAsyncEnumerable<int> Counter(int max)
    {
        for (var i = 0; i < max; i++)
        {
            yield return i;
            await Task.Yield();
        }
    }

    public async Task PushNotify(string message) =>
        await Clients.Caller.SendAsync("Notify", message).ConfigureAwait(false);

    public async Task<int> HoldInvoke(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    public async IAsyncEnumerable<int> Hold([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 0;
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }
}
