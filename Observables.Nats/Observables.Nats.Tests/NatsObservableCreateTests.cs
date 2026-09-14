using NATS.Client.Core;
using Observables.Nats.Tests.Infrastructure;
using R3;

namespace Observables.Nats.Tests;

public sealed class NatsObservableCreateTests
{
    [Fact]
    public async Task FromSubscribe_message_error_completes_with_failure_without_onnext()
    {
        var connection = ErrorNats.CreateWithDeserializeError();
        var observer = new RecordingObserver<string>();

        using var subscription = NatsObservable.FromSubscribe<string>(connection, "orders").Subscribe(observer);

        var result = await observer.Completed.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Empty(observer.Values);
        Assert.True(result.IsFailure);
        Assert.IsType<NatsDeserializeException>(result.Exception);
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
}
