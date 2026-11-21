using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace Observables.SignalR.Tests.Infrastructure;

/// <summary>In-process SignalR hub backing <see cref="Contracts.IE2EHub"/> client contract.</summary>
public sealed class E2ETestHub : Hub
{
    public Task<int> Add(int a, int b) => Task.FromResult(a + b);

    public Task EchoSend(string text) => Task.CompletedTask;

    public int[] Read8(int a, int b, int c, int d, int e, int f, int g, int h) =>
        [a, b, c, d, e, f, g, h];

    public int[] Read9(int a, int b, int c, int d, int e, int f, int g, int h, int i) =>
        [a, b, c, d, e, f, g, h, i];

    public Task Notify8(int a, int b, int c, int d, int e, int f, int g, int h) =>
        Clients.Caller.SendAsync("ReceivedArgs", Read8(a, b, c, d, e, f, g, h));

    public Task Notify9(int a, int b, int c, int d, int e, int f, int g, int h, int i) =>
        Clients.Caller.SendAsync("ReceivedArgs", Read9(a, b, c, d, e, f, g, h, i));

    public IAsyncEnumerable<int[]> Stream8(int a, int b, int c, int d, int e, int f, int g, int h) =>
        Single(Read8(a, b, c, d, e, f, g, h));

    public IAsyncEnumerable<int[]> Stream9(int a, int b, int c, int d, int e, int f, int g, int h, int i) =>
        Single(Read9(a, b, c, d, e, f, g, h, i));

    static async IAsyncEnumerable<int[]> Single(int[] args)
    {
        await Task.Yield();
        yield return args;
    }

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

    public async Task<int> HoldInvoke()
    {
        await Clients.Caller.SendAsync("InvokeStarted", Context.ConnectionAborted).ConfigureAwait(false);
        await Task.Delay(Timeout.Infinite, Context.ConnectionAborted).ConfigureAwait(false);
        return 0;
    }

    public async IAsyncEnumerable<int> Hold([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 0;
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
    }
}
