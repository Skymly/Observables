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
            await SignalRProtocol.InvokeAsync<T>(connection, methodName, args, cancellationToken, ct).ConfigureAwait(false));

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
            await SignalRProtocol.SendAsync(connection, methodName, args, cancellationToken, ct).ConfigureAwait(false);
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
            await SignalRProtocol
                .StreamAsync<T>(connection, methodName, args, observer.OnNext, observer.OnCompleted, cancellationToken, ct)
                .ConfigureAwait(false);
        });

    public static Observable<T> FromOn<T>(HubConnection connection, string methodName) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            using var subscription = SignalRProtocol.SubscribeOn<T>(connection, methodName, observer.OnNext);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        });
}
