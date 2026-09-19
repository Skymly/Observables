using System.Text;
using Grpc.Core;
using Observables.Grpc.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace Observables.Grpc.Reactive.Tests;

public sealed class GrpcAdapterTerminalTests
{
    [Fact]
    public async Task FromServerStreaming_remote_cancelled_without_local_cancel_errors()
    {
        var reader = new FailingStreamReader<string>(new RpcException(new Status(StatusCode.Cancelled, "peer")));
        var invoker = new FakeCallInvoker(reader);
        var errored = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = SystemReactiveGrpcAdapter
            .FromServerStreaming(invoker, CreateMethod(MethodType.ServerStreaming), "ping", TestContext.Current.CancellationToken)
            .Subscribe(_ => { }, ex => errored.TrySetResult(ex), () => completed.TrySetResult());

        var finished = await Task.WhenAny(errored.Task, completed.Task)
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Same(errored.Task, finished);
        var error = await errored.Task;
        var rpc = Assert.IsType<RpcException>(error);
        Assert.Equal(StatusCode.Cancelled, rpc.StatusCode);
    }

    [Fact]
    public async Task FromClientStreaming_never_requests_still_complete_when_response_is_ready()
    {
        var invoker = new FakeCallInvoker();
        var reply = await SystemReactiveGrpcAdapter
            .FromClientStreaming(
                invoker,
                CreateMethod(MethodType.ClientStreaming),
                Observable.Never<string>(), TestContext.Current.CancellationToken)
            .Timeout(TimeSpan.FromSeconds(2))
            .FirstAsync()
            .ToTask();

        Assert.Equal("reply", reply);
        await invoker.CallDisposed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }

    static Method<string, string> CreateMethod(MethodType type) =>
        new(
            type,
            "test.Service",
            "Method",
            Marshallers.Create<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString),
            Marshallers.Create<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString));

    sealed class FailingStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        readonly Exception _error;

        public FailingStreamReader(Exception error) => _error = error;

        public T Current => throw new InvalidOperationException("no current value");

        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromException<bool>(_error);
    }

    sealed class NoopClientStreamWriter<T> : IClientStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task CompleteAsync() => Task.CompletedTask;

        public Task WriteAsync(T message) => Task.CompletedTask;
    }

    sealed class FakeCallInvoker(object? reader = null) : CallInvoker
    {
        public TaskCompletionSource CallDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
        {
            var writer = (IClientStreamWriter<TRequest>)(object)new NoopClientStreamWriter<TRequest>();
            return new AsyncClientStreamingCall<TRequest, TResponse>(
                writer,
                Task.FromResult((TResponse)(object)"reply"),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options) =>
            throw new NotSupportedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            new(
                (IAsyncStreamReader<TResponse>)reader!,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();
    }
}
