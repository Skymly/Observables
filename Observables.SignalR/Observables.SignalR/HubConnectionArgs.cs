using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR;

/// <summary>Invokes hub methods using per-argument overloads so args bind correctly on the server.</summary>
public static class HubConnectionArgs
{
    public static Task<T> InvokeAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        args.Length switch
        {
            0 => connection.InvokeAsync<T>(methodName, cancellationToken),
            1 => connection.InvokeAsync<T>(methodName, args[0], cancellationToken),
            2 => connection.InvokeAsync<T>(methodName, args[0], args[1], cancellationToken),
            3 => connection.InvokeAsync<T>(methodName, args[0], args[1], args[2], cancellationToken),
            4 => connection.InvokeAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                cancellationToken),
            5 => connection.InvokeAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                cancellationToken),
            6 => connection.InvokeAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                cancellationToken),
            7 => connection.InvokeAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                cancellationToken),
            8 => connection.InvokeAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                args[7],
                cancellationToken),
            _ => connection.InvokeAsync<T>(methodName, args, cancellationToken),
        };

    public static Task SendAsync(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        args.Length switch
        {
            0 => connection.SendAsync(methodName, cancellationToken),
            1 => connection.SendAsync(methodName, args[0], cancellationToken),
            2 => connection.SendAsync(methodName, args[0], args[1], cancellationToken),
            3 => connection.SendAsync(methodName, args[0], args[1], args[2], cancellationToken),
            4 => connection.SendAsync(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                cancellationToken),
            5 => connection.SendAsync(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                cancellationToken),
            6 => connection.SendAsync(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                cancellationToken),
            7 => connection.SendAsync(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                cancellationToken),
            8 => connection.SendAsync(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                args[7],
                cancellationToken),
            _ => connection.SendAsync(methodName, args, cancellationToken),
        };

    public static IAsyncEnumerable<T> StreamAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken) =>
        args.Length switch
        {
            0 => connection.StreamAsync<T>(methodName, cancellationToken),
            1 => connection.StreamAsync<T>(methodName, args[0], cancellationToken),
            2 => connection.StreamAsync<T>(methodName, args[0], args[1], cancellationToken),
            3 => connection.StreamAsync<T>(methodName, args[0], args[1], args[2], cancellationToken),
            4 => connection.StreamAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                cancellationToken),
            5 => connection.StreamAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                cancellationToken),
            6 => connection.StreamAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                cancellationToken),
            7 => connection.StreamAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                cancellationToken),
            8 => connection.StreamAsync<T>(
                methodName,
                args[0],
                args[1],
                args[2],
                args[3],
                args[4],
                args[5],
                args[6],
                args[7],
                cancellationToken),
            _ => connection.StreamAsync<T>(methodName, args, cancellationToken),
        };
}
