using System.Text;
using Grpc.Core;
using Observables.Grpc.Reactive;
using System.Reactive.Subjects;

namespace Observables.Grpc.Reactive.Tests;

public sealed class SystemReactiveGrpcAdapterTests
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task FromServerStreaming_linked_token_lives_until_dispose()
    {
        var reader = new HangingStreamReader<string>("ready");
        var invoker = new FakeCallInvoker(reader);
        await AssertLinkedTokenLivesUntilDispose(
            SystemReactiveGrpcAdapter.FromServerStreaming(
                invoker,
                CreateMethod(MethodType.ServerStreaming),
                "request",
                TestContext.Current.CancellationToken),
            invoker,
            reader);
    }

    [Fact]
    public async Task FromDuplexStreaming_linked_token_lives_until_dispose()
    {
        var reader = new HangingStreamReader<string>("ready");
        var writer = new NoopClientStreamWriter<string>();
        var invoker = new FakeCallInvoker(reader, writer);
        var requests = new Subject<string>();
        await AssertLinkedTokenLivesUntilDispose(
            SystemReactiveGrpcAdapter.FromDuplexStreaming(
                invoker,
                CreateMethod(MethodType.DuplexStreaming),
                requests,
                TestContext.Current.CancellationToken),
            invoker,
            reader);
    }

    static async Task AssertLinkedTokenLivesUntilDispose(
        IObservable<string> stream,
        FakeCallInvoker invoker,
        HangingStreamReader<string> reader)
    {
        var completed = 0;
        var errored = 0;
        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = stream.Subscribe(
            value => ready.TrySetResult(value),
            _ => Interlocked.Exchange(ref errored, 1),
            () => Interlocked.Exchange(ref completed, 1));

        using var cts = new CancellationTokenSource(DefaultTimeout);
        Assert.Equal("ready", await ready.Task.WaitAsync(cts.Token));
        Assert.False(invoker.CallToken.IsCancellationRequested);

        subscription.Dispose();
        await reader.MoveNextCancelled.Task.WaitAsync(cts.Token);

        Assert.Equal(0, Volatile.Read(ref completed));
        Assert.Equal(0, Volatile.Read(ref errored));
    }

    static Method<string, string> CreateMethod(MethodType type) =>
        new(
            type,
            "test.Service",
            "Method",
            Marshallers.Create<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString),
            Marshallers.Create<string>(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString));

    sealed class HangingStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        readonly T first;
        int moved;

        public HangingStreamReader(T first) => this.first = first;

        public T Current { get; private set; } = null!;

        public TaskCompletionSource MoveNextCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref moved) == 1)
            {
                Current = first;
                return true;
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                MoveNextCancelled.TrySetResult();
                throw;
            }
        }
    }

    sealed class NoopClientStreamWriter<T> : IClientStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task CompleteAsync() => Task.CompletedTask;

        public Task WriteAsync(T message) => Task.CompletedTask;
    }

    sealed class FakeCallInvoker : CallInvoker
    {
        readonly object reader;
        readonly object? writer;

        public FakeCallInvoker(object reader, object? writer = null)
        {
            this.reader = reader;
            this.writer = writer;
        }

        public CancellationToken CallToken { get; private set; }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options) =>
            throw new NotSupportedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
        {
            CallToken = options.CancellationToken;
            return new AsyncDuplexStreamingCall<TRequest, TResponse>(
                (IClientStreamWriter<TRequest>)writer!,
                (IAsyncStreamReader<TResponse>)reader,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            CallToken = options.CancellationToken;
            return new AsyncServerStreamingCall<TResponse>(
                (IAsyncStreamReader<TResponse>)reader,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request) =>
            throw new NotSupportedException();
    }
}
