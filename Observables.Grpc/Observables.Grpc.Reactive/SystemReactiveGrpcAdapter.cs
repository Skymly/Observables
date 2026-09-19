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
            catch (OperationCanceledException oce) when (oce.CancellationToken == ct)
            {
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
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

            var writer = new SerializedClientStreamWriter<TRequest>(call.RequestStream);
            var writeCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelReg = linked.Token.Register(() => writeCompleted.TrySetCanceled(linked.Token));
            using var subscription = requests.Subscribe(
                item => GrpcProtocol.ObserveWrite(writer.WriteAsync(item, linked.Token), writeCompleted),
                ex => writeCompleted.TrySetException(ex),
                () => writeCompleted.TrySetResult(true));

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

                var writer = new SerializedClientStreamWriter<TRequest>(call.RequestStream);
                var writeCompleted = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using var cancelReg = linked.Token.Register(() => writeCompleted.TrySetCanceled(linked.Token));
                using var subscription = requests.Subscribe(
                    item => GrpcProtocol.ObserveWrite(writer.WriteAsync(item, linked.Token), writeCompleted),
                    ex => writeCompleted.TrySetException(ex),
                    () => writeCompleted.TrySetResult(true));

                await GrpcProtocol
                    .PumpStreamingCallAsync(writer, writeCompleted.Task, call.ResponseStream, observer.OnNext, linked.Token)
                    .ConfigureAwait(false);
                observer.OnCompleted();
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == ct)
            {
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
}
