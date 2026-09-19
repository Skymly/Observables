using System.Text;
using Grpc.Core;
using R3;

namespace Observables.Grpc.Tests;

public sealed class GrpcObservableLifecycleTests
{
    [Fact]
    public async Task FromClientStreaming_request_failure_completes_with_failure()
    {
        var invoker = new FakeCallInvoker();
        var observer = new RecordingObserver<string>();

        using var subscription = GrpcObservable
            .FromClientStreaming(
                invoker,
                CreateMethod(MethodType.ClientStreaming),
                new FailedRequests(),
                TestContext.Current.CancellationToken)
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Empty(observer.Values);
        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("request-failed", result.Exception.Message);
    }

    [Fact]
    public async Task FromDuplexStreaming_response_failure_unblocks_when_request_is_open()
    {
        var reader = new FailingStreamReader<string>(new RpcException(new Status(StatusCode.Unavailable, "down")));
        var writer = new NoopClientStreamWriter<string>();
        var invoker = new FakeCallInvoker(reader, writer);
        using var requests = new Subject<string>();
        var observer = new RecordingObserver<string>();

        using var subscription = GrpcObservable
            .FromDuplexStreaming(
                invoker,
                CreateMethod(MethodType.DuplexStreaming),
                requests,
                TestContext.Current.CancellationToken)
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.IsType<RpcException>(result.Exception);
        Assert.Equal(StatusCode.Unavailable, ((RpcException)result.Exception).StatusCode);
        Assert.True(invoker.CallDisposed.Task.IsCompleted);
    }

    [Fact]
    public async Task FromUnary_disposes_the_call()
    {
        var invoker = new FakeCallInvoker();

        var reply = await GrpcObservable
            .FromUnary(invoker, CreateMethod(MethodType.Unary), "ping", TestContext.Current.CancellationToken)
            .FirstAsync(TestContext.Current.CancellationToken);

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

    sealed class FailedRequests : Observable<string>
    {
        protected override IDisposable SubscribeCore(Observer<string> observer)
        {
            observer.OnCompleted(Result.Failure(new InvalidOperationException("request-failed")));
            return Disposable.Empty;
        }
    }

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

    sealed class CompletingClientStreamWriter<T> : IClientStreamWriter<T>
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WriteOptions? WriteOptions { get; set; }

        public Task CompleteAsync()
        {
            Completed.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }

    sealed class FakeCallInvoker : CallInvoker
    {
        readonly object? _reader;
        readonly object? _writer;

        readonly bool _completeClientStreamingImmediately;

        public FakeCallInvoker(object? reader = null, object? writer = null, bool completeClientStreamingImmediately = false)
        {
            _reader = reader;
            _writer = writer;
            _completeClientStreamingImmediately = completeClientStreamingImmediately;
        }

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
            if (_completeClientStreamingImmediately)
            {
                var immediateWriter = _writer is IClientStreamWriter<TRequest> typedImmediate
                    ? typedImmediate
                    : (IClientStreamWriter<TRequest>)(object)new NoopClientStreamWriter<TRequest>();
                return new AsyncClientStreamingCall<TRequest, TResponse>(
                    immediateWriter,
                    Task.FromResult((TResponse)(object)"reply"),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => CallDisposed.TrySetResult());
            }

            var completing = new CompletingClientStreamWriter<TRequest>();
            var response = completing.Completed.Task.ContinueWith(
                static _ => (TResponse)(object)"reply",
                TaskScheduler.Default);
            return new AsyncClientStreamingCall<TRequest, TResponse>(
                completing,
                response,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options) =>
            new(
                (IClientStreamWriter<TRequest>)_writer!,
                (IAsyncStreamReader<TResponse>)_reader!,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            new(
                (IAsyncStreamReader<TResponse>)_reader!,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            new(
                Task.FromResult((TResponse)(object)"reply"),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed.TrySetResult());
    }

    sealed class RecordingObserver<T> : Observer<T>
    {
        readonly TaskCompletionSource<Result> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly List<T> _values = [];

        public IReadOnlyList<T> Values => _values;

        public Task<Result> Completed => _completed.Task;

        protected override void OnNextCore(T value) => _values.Add(value);

        protected override void OnErrorResumeCore(Exception error)
        {
        }

        protected override void OnCompletedCore(Result result) => _completed.TrySetResult(result);
    }

    [Fact]
    public async Task FromServerStreaming_remote_cancelled_without_local_cancel_fails()
    {
        var reader = new FailingStreamReader<string>(new RpcException(new Status(StatusCode.Cancelled, "peer")));
        var invoker = new FakeCallInvoker(reader);
        var observer = new RecordingObserver<string>();

        using var subscription = GrpcObservable
            .FromServerStreaming(invoker, CreateMethod(MethodType.ServerStreaming), "ping", TestContext.Current.CancellationToken)
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.IsType<RpcException>(result.Exception);
        Assert.Equal(StatusCode.Cancelled, ((RpcException)result.Exception).StatusCode);
    }

    [Fact]
    public async Task FromClientStreaming_never_requests_still_complete_when_response_is_ready()
    {
        var invoker = new FakeCallInvoker(completeClientStreamingImmediately: true);
        var observer = new RecordingObserver<string>();

        using var subscription = GrpcObservable
            .FromClientStreaming(
                invoker,
                CreateMethod(MethodType.ClientStreaming),
                Observable.Never<string>(), TestContext.Current.CancellationToken)
            .Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(result.IsFailure);
        Assert.Equal(new[] { "reply" }, observer.Values);
        await invoker.CallDisposed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    }
}