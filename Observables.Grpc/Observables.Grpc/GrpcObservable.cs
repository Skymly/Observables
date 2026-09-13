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
            await GrpcProtocol.UnaryAsync(invoker, method, request, cancellationToken, ct).ConfigureAwait(false));

    public static Observable<TResponse> FromServerStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        R3AsyncCreate.Create<TResponse>(async (observer, ct) =>
        {
            try
            {
                await GrpcProtocol
                    .ReadServerStreamAsync(invoker, method, request, observer.OnNext, observer.OnCompleted, cancellationToken, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == ct || ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                throw new OperationCanceledException(ct);
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

            var writer = new SerializedClientStreamWriter<TRequest>(call.RequestStream);
            var writeCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelReg = linked.Token.Register(() => writeCompleted.TrySetCanceled(linked.Token));
            using var subscription = requests.Subscribe(
                (Action<TRequest>)(item =>
                    GrpcProtocol.ObserveWrite(writer.WriteAsync(item, linked.Token), writeCompleted)),
                (Action<Exception>)(ex => writeCompleted.TrySetException(ex)),
                (Action<Result>)(result => GrpcProtocol.ObserveCompleted(result, writeCompleted)));

            await writeCompleted.Task.ConfigureAwait(false);
            await writer.CompleteAsync().ConfigureAwait(false);
            return await call.ResponseAsync.ConfigureAwait(false);
        });

    public static Observable<TResponse> FromDuplexStreaming<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        Observable<TRequest> requests,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class =>
        R3AsyncCreate.Create<TResponse>(async (observer, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct);
            using var call = invoker.AsyncDuplexStreamingCall(
                method,
                host: null,
                options: new CallOptions(cancellationToken: linked.Token));

            var writer = new SerializedClientStreamWriter<TRequest>(call.RequestStream);
            var writeCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelReg = linked.Token.Register(() => writeCompleted.TrySetCanceled(linked.Token));
            using var subscription = requests.Subscribe(
                (Action<TRequest>)(item =>
                    GrpcProtocol.ObserveWrite(writer.WriteAsync(item, linked.Token), writeCompleted)),
                (Action<Exception>)(ex => writeCompleted.TrySetException(ex)),
                (Action<Result>)(result => GrpcProtocol.ObserveCompleted(result, writeCompleted)));

            try
            {
                await GrpcProtocol
                    .PumpStreamingCallAsync(writer, writeCompleted.Task, call.ResponseStream, observer.OnNext, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == linked.Token || ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                throw new OperationCanceledException(ct);
            }
        });
}
