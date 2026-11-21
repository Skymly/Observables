using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR;

/// <summary>Forwards each argument to the corresponding parameter of the Hub method.</summary>
public static class HubConnectionArgs
{
    public static Task<T> InvokeAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        connection.InvokeCoreAsync<T>(methodName, args, cancellationToken);

    public static Task SendAsync(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        connection.SendCoreAsync(methodName, args, cancellationToken);

    public static IAsyncEnumerable<T> StreamAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        connection.StreamAsyncCore<T>(methodName, args, cancellationToken);
}
