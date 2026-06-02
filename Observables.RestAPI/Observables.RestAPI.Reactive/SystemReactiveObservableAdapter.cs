using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Observables.RestAPI.Reactive;

/// <summary>
/// Bridges Observables.RestAPI async request cores to <see cref="IObservable{T}"/>.
/// </summary>
public static class SystemReactiveObservableAdapter
{
    /// <summary>
    /// Creates a cold <see cref="IObservable{T}"/> from an async factory.
    /// </summary>
    public static IObservable<T> FromAsync<T>(Func<CancellationToken, ValueTask<T>> asyncFactory) =>
        Observable.FromAsync(ct => asyncFactory(ct).AsTask());
}
