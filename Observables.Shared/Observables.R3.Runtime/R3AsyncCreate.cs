using R3;

namespace Observables;

/// <summary>
/// R3 1.3.0 <c>Observable.Create</c> does not await or catch the async subscribe
/// delegate. This helper matches <c>Observable.CreateFrom</c>: run the delegate
/// with <c>async void</c>, complete successfully when it returns, and complete
/// with <c>OnCompleted(Result.Failure(ex))</c> when it throws. Subscription-token
/// cancellation is silent; other cancellations terminate the observer.
/// </summary>
internal static class R3AsyncCreate
{
    internal static Observable<T> Create<T>(Func<Observer<T>, CancellationToken, ValueTask> subscribe)
    {
        if (subscribe is null)
        {
            throw new ArgumentNullException(nameof(subscribe));
        }

        return new AsyncCreateObservable<T>(subscribe);
    }

    sealed class AsyncCreateObservable<T>(Func<Observer<T>, CancellationToken, ValueTask> subscribe) : Observable<T>
    {
        protected override IDisposable SubscribeCore(Observer<T> observer)
        {
            var cancellation = new CancellationTokenSource();
            RunAsync(observer.Wrap(), cancellation.Token);
            return new Subscription(cancellation);
        }

        async void RunAsync(Observer<T> observer, CancellationToken cancellationToken)
        {
            try
            {
                await subscribe(observer, cancellationToken).ConfigureAwait(false);
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken)
                {
                    return;
                }

                observer.OnCompleted(Result.Failure(ex));
            }
        }
    }

    sealed class Subscription(CancellationTokenSource cancellation) : IDisposable
    {
        public void Dispose() => cancellation.Cancel();
    }
}
