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
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            return await HubConnectionArgs.InvokeAsync<T>(connection, methodName, args, linked.Token)
                .ConfigureAwait(false);
        });

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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            await HubConnectionArgs.SendAsync(connection, methodName, args, linked.Token).ConfigureAwait(false);
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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            try
            {
                await foreach (var item in HubConnectionArgs
                                   .StreamAsync<T>(connection, methodName, args, linked.Token)
                                   .ConfigureAwait(false))
                {
                    observer.OnNext(item);
                }

                observer.OnCompleted();
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
            var subscription = connection.On<T>(methodName, observer.OnNext);
            return System.Reactive.Disposables.Disposable.Create(subscription.Dispose);
        });
}
