using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR;

internal static class SignalRProtocol
{
    internal static async Task<T> InvokeAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        return await HubConnectionArgs.InvokeAsync<T>(connection, methodName, args, linked.Token).ConfigureAwait(false);
    }

    internal static async Task SendAsync(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await HubConnectionArgs.SendAsync(connection, methodName, args, linked.Token).ConfigureAwait(false);
    }

    internal static async Task StreamAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        Action<T> onNext,
        Action onCompleted,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await foreach (var item in HubConnectionArgs
                           .StreamAsync<T>(connection, methodName, args, linked.Token)
                           .ConfigureAwait(false))
        {
            onNext(item);
        }

        onCompleted();
    }

    internal static IDisposable SubscribeOn<T>(HubConnection connection, string methodName, Action<T> onNext) =>
        connection.On<T>(methodName, onNext);
}
