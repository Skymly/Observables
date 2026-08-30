using System.Reactive.Linq;
using Grpc.Core;
using Observables.Grpc;

namespace Observables.Grpc.Reactive;

/// <summary>Bridges <see cref="CallInvoker"/> APIs to <see cref="IObservable{T}"/>.</summary>
public static class SystemReactiveGrpcAdapter
{
    public static IObservable<TResponse> FromUnary<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.FromAsync(async ct =>
            await GrpcProtocol.UnaryAsync(invoker, method, request, cancellationToken, ct).ConfigureAwait(false));

    public static IObservable<TResponse> FromServerStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.Create<TResponse>(async (observer, ct) =>
        {
            try
            {
                await GrpcProtocol
                    .ReadServerStreamAsync(invoker, method, request, observer.OnNext, observer.OnCompleted, cancellationToken, ct)
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

    public static IObservable<TResponse> FromClientStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        IObservable<TRequest> requests,
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
                item => WriteRequest(call.RequestStream, item),
                ex => writeCompleted.TrySetException(ex),
                () => writeCompleted.TrySetResult(true));

            await writeCompleted.Task.ConfigureAwait(false);
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            return await call.ResponseAsync.ConfigureAwait(false);
        });

    public static IObservable<TResponse> FromDuplexStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        IObservable<TRequest> requests,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        Observable.Create<TResponse>(async (observer, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            try
            {
                using var call = invoker.AsyncDuplexStreamingCall(
                    method,
                    host: null,
                    options: new CallOptions(cancellationToken: linked.Token));

                using var subscription = requests.Subscribe(
                    item => WriteRequest(call.RequestStream, item),
                    () => _ = call.RequestStream.CompleteAsync());

                while (await call.ResponseStream.MoveNext(linked.Token).ConfigureAwait(false))
                {
                    observer.OnNext(call.ResponseStream.Current);
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

    static void WriteRequest<TRequest>(IClientStreamWriter<TRequest> stream, TRequest item) =>
        _ = stream.WriteAsync(item);
}
