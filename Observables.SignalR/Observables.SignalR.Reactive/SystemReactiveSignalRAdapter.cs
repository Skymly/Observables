using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

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
        Observable.FromAsync(ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            return connection.InvokeAsync<T>(methodName, args, linked.Token);
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
            await connection.SendAsync(methodName, args, linked.Token).ConfigureAwait(false);
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
        Observable.Create<T>(observer =>
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = PumpStreamAsync(connection, methodName, args, observer, cts.Token);
            return Disposable.Create(cts.Cancel);
        });

    static async Task PumpStreamAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        IObserver<T> observer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in connection
                               .StreamAsync<T>(methodName, args, cancellationToken)
                               .ConfigureAwait(false))
            {
                observer.OnNext(item);
            }

            observer.OnCompleted();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }

    public static IObservable<T> FromOn<T>(HubConnection connection, string methodName) =>
        Observable.Create<T>(observer =>
        {
            var subscription = connection.On<T>(methodName, observer.OnNext);
            return Disposable.Create(subscription.Dispose);
        });
}
