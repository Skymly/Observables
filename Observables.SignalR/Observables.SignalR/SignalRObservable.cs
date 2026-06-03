using Microsoft.AspNetCore.SignalR.Client;
using R3;

namespace Observables.SignalR;

/// <summary>Bridges SignalR client APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class SignalRObservable
{
    public static Observable<T> FromInvoke<T>(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromInvoke<T>(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static Observable<T> FromInvoke<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            return await connection
                .InvokeAsync<T>(methodName, args, linked.Token)
                .ConfigureAwait(false);
        });

    public static Observable<Unit> FromSend(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromSend(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static Observable<Unit> FromSend(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await connection.SendAsync(methodName, args, linked.Token).ConfigureAwait(false);
            return Unit.Default;
        });

    public static Observable<T> FromStream<T>(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromStream<T>(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static Observable<T> FromStream<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await foreach (var item in connection
                               .StreamAsync<T>(methodName, args, linked.Token)
                               .ConfigureAwait(false))
            {
                observer.OnNext(item);
            }

            observer.OnCompleted();
        });

    public static Observable<T> FromOn<T>(HubConnection connection, string methodName) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            var subscription = connection.On<T>(methodName, observer.OnNext);
            try
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            }
            finally
            {
                subscription.Dispose();
            }
        });
}
