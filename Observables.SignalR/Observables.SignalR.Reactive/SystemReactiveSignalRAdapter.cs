using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR;

namespace Observables.SignalR.Reactive;

/// <summary>Bridges SignalR client APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveSignalRAdapter
{
    public static IObservable<T> FromInvoke<T>(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromInvoke<T>(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static IObservable<T> FromInvoke<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
            await SignalRProtocol.InvokeAsync<T>(connection, methodName, args, cancellationToken, ct).ConfigureAwait(false));

    public static IObservable<System.Reactive.Unit> FromSend(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromSend(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static IObservable<System.Reactive.Unit> FromSend(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.FromAsync(async ct =>
        {
            await SignalRProtocol.SendAsync(connection, methodName, args, cancellationToken, ct).ConfigureAwait(false);
            return System.Reactive.Unit.Default;
        });

    public static IObservable<T> FromStream<T>(
        HubConnection connection,
        string methodName,
        CancellationToken cancellationToken = default) =>
        FromStream<T>(connection, methodName, Array.Empty<object?>(), cancellationToken);

    public static IObservable<T> FromStream<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken cancellationToken = default) =>
        Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await SignalRProtocol
                    .StreamAsync<T>(connection, methodName, args, observer.OnNext, observer.OnCompleted, cancellationToken, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });

    public static IObservable<T> FromOn<T>(HubConnection connection, string methodName) =>
        Observable.Create<T>(observer =>
        {
            var subscription = SignalRProtocol.SubscribeOn<T>(connection, methodName, observer.OnNext);
            return System.Reactive.Disposables.Disposable.Create(subscription.Dispose);
        });
}
