using Grpc.Core;
using R3;

namespace Observables.Grpc;

/// <summary>Bridges <see cref="CallInvoker"/> APIs to R3 <see cref="Observable{T}"/>.</summary>
public static class GrpcObservable
{
    public static Observable<TResponse> FromUnary<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            var call = invoker.AsyncUnaryCall(
                method,
                host: null,
                options: new CallOptions(cancellationToken: linked.Token),
                request);
            return await call.ResponseAsync.ConfigureAwait(false);
        });

    public static Observable<TResponse> FromServerStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.Create<TResponse>(async (observer, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            try
            {
                using var call = invoker.AsyncServerStreamingCall(
                    method,
                    host: null,
                    options: new CallOptions(cancellationToken: linked.Token),
                    request);

                while (await call.ResponseStream.MoveNext(linked.Token).ConfigureAwait(false))
                {
                    observer.OnNext(call.ResponseStream.Current);
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        });

    public static Observable<TResponse> FromClientStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        Observable<TRequest> requests,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.FromAsync(async ct =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            using var call = invoker.AsyncClientStreamingCall(
                method,
                host: null,
                options: new CallOptions(cancellationToken: linked.Token));

            var writeCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = requests.Subscribe(
                (Action<TRequest>)(item => _ = WriteRequestAsync(call.RequestStream, item, linked.Token)),
                (Action<Exception>)(ex => writeCompleted.TrySetException(ex)),
                (Action<Result>)(_ => writeCompleted.TrySetResult(true)));

            await writeCompleted.Task.ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            return await call.ResponseAsync.ConfigureAwait(false);
        });

    public static Observable<TResponse> FromDuplexStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        Observable<TRequest> requests,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.Create<TResponse>(async (observer, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            using var call = invoker.AsyncDuplexStreamingCall(
                method,
                host: null,
                options: new CallOptions(cancellationToken: linked.Token));

            var writeCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = requests.Subscribe(
                (Action<TRequest>)(item => _ = WriteRequestAsync(call.RequestStream, item, linked.Token)),
                (Action<Exception>)(ex => writeCompleted.TrySetException(ex)),
                (Action<Result>)(_ => writeCompleted.TrySetResult(true)));

            try
            {
                await writeCompleted.Task.ConfigureAwait(false);
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);

                while (await call.ResponseStream.MoveNext(linked.Token).ConfigureAwait(false))
                {
                    observer.OnNext(call.ResponseStream.Current);
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                observer.OnErrorResume(ex);
            }
        });

    static async Task WriteRequestAsync<TRequest>(
        IClientStreamWriter<TRequest> stream,
        TRequest item,
        CancellationToken cancellationToken)
    {
#if NETSTANDARD2_0
        await stream.WriteAsync(item).ConfigureAwait(false);
#else
        await stream.WriteAsync(item, cancellationToken).ConfigureAwait(false);
#endif
    }
}
