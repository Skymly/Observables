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
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
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
                (Action<Result>)(result => ObserveCompleted(result, writeCompleted)));

            var responseTask = call.ResponseAsync;
            var finished = await Task.WhenAny(responseTask, writeCompleted.Task).ConfigureAwait(false);
            if (finished == responseTask)
            {
                subscription.Dispose();
                linked.Cancel();
                return await responseTask.ConfigureAwait(false);
            }

            await writeCompleted.Task.ConfigureAwait(false);
            await writer.CompleteAsync().ConfigureAwait(false);
            return await responseTask.ConfigureAwait(false);
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
                (Action<Result>)(result => ObserveCompleted(result, writeCompleted)));

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
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
        });

    // R3 collapses completion and failure into one Result callback, so the request-stream writer needs this
    // shim; the System.Reactive adapter gets separate onError / onCompleted delegates and inlines the same two
    // lines. Lives here rather than in GrpcProtocol so the neutral runtime stays free of R3.
    static void ObserveCompleted(Result result, TaskCompletionSource<bool> writeCompleted)
    {
        if (result.IsFailure)
        {
            writeCompleted.TrySetException(result.Exception ?? new InvalidOperationException("gRPC request source failed."));
            return;
        }

        writeCompleted.TrySetResult(true);
    }
}
