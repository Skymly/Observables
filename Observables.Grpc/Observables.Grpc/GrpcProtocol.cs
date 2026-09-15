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
        using var call = invoker.AsyncUnaryCall(
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

        await ReadResponsesAsync(call.ResponseStream, onNext, linked.Token).ConfigureAwait(false);
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

    internal static async Task ReadResponsesAsync<TResponse>(
        IAsyncStreamReader<TResponse> stream,
        Action<TResponse> onNext,
        CancellationToken cancellationToken)
    {
        while (await stream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            onNext(stream.Current);
        }
    }

    internal static async Task PumpStreamingCallAsync<TRequest, TResponse>(
        SerializedClientStreamWriter<TRequest> writer,
        Task writeCompleted,
        IAsyncStreamReader<TResponse> responseStream,
        Action<TResponse> onNext,
        CancellationToken cancellationToken)
    {
        var readTask = ReadResponsesAsync(responseStream, onNext, cancellationToken);
        var finished = await Task.WhenAny(writeCompleted, readTask).ConfigureAwait(false);
        if (finished == readTask)
        {
            await readTask.ConfigureAwait(false);
            return;
        }

        await writeCompleted.ConfigureAwait(false);
        await writer.CompleteAsync().ConfigureAwait(false);
        await readTask.ConfigureAwait(false);
    }

    internal static void ObserveWrite(Task write, TaskCompletionSource<bool> writeCompleted)
    {
        if (write.IsCompleted)
        {
            if (write.IsFaulted)
            {
                writeCompleted.TrySetException(write.Exception!.GetBaseException());
            }

            return;
        }

        write.ContinueWith(
            static (task, state) =>
            {
                var tcs = (TaskCompletionSource<bool>)state!;
                if (task.IsFaulted)
                {
                    tcs.TrySetException(task.Exception!.GetBaseException());
                }
            },
            writeCompleted,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

internal sealed class SerializedClientStreamWriter<TRequest>
{
    readonly IClientStreamWriter<TRequest> stream;
    readonly object gate = new();
    Task pending = Task.CompletedTask;

    internal SerializedClientStreamWriter(IClientStreamWriter<TRequest> stream) => this.stream = stream;

    internal Task WriteAsync(TRequest item, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            pending = WriteAfterAsync(pending, item, cancellationToken);
            return pending;
        }
    }

    internal async Task CompleteAsync()
    {
        Task toAwait;
        lock (gate)
        {
            toAwait = pending;
        }

        await toAwait.ConfigureAwait(false);
        await stream.CompleteAsync().ConfigureAwait(false);
    }

    async Task WriteAfterAsync(Task previous, TRequest item, CancellationToken cancellationToken)
    {
        await previous.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await GrpcProtocol.WriteRequestAsync(stream, item, cancellationToken).ConfigureAwait(false);
    }
}
