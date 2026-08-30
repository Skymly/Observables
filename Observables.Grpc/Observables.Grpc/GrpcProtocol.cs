using Grpc.Core;

namespace Observables.Grpc;

internal static class GrpcProtocol
{
    internal static async Task<TResponse> UnaryAsync<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        CancellationToken userToken,
        CancellationToken pumpToken)
        where TRequest : class
        where TResponse : class
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        var call = invoker.AsyncUnaryCall(
            method,
            host: null,
            options: new CallOptions(cancellationToken: linked.Token),
            request);
        return await call.ResponseAsync.ConfigureAwait(false);
    }

    internal static async Task ReadServerStreamAsync<TRequest, TResponse>(
        CallInvoker invoker,
        Method<TRequest, TResponse> method,
        TRequest request,
        Action<TResponse> onNext,
        Action onCompleted,
        CancellationToken userToken,
        CancellationToken pumpToken)
        where TRequest : class
        where TResponse : class
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        using var call = invoker.AsyncServerStreamingCall(
            method,
            host: null,
            options: new CallOptions(cancellationToken: linked.Token),
            request);

        while (await call.ResponseStream.MoveNext(linked.Token).ConfigureAwait(false))
        {
            onNext(call.ResponseStream.Current);
        }

        onCompleted();
    }

    internal static async Task WriteRequestAsync<TRequest>(
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
