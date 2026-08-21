using System.Reflection;
using StackExchange.Redis;

namespace Observables.Redis.Tests.Infrastructure;

internal static class HangingRedis
{
    public static (IConnectionMultiplexer Multiplexer, Task PublishStarted) CreateForPublish()
    {
        var publishStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = DispatchProxy.Create<ISubscriber, HangingSubscriberProxy>();
        ((HangingSubscriberProxy)(object)subscriber).PublishStarted = publishStarted;

        var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, HangingMultiplexerProxy>();
        ((HangingMultiplexerProxy)(object)multiplexer).Subscriber = subscriber;
        return (multiplexer, publishStarted.Task);
    }

    public class HangingMultiplexerProxy : DispatchProxy
    {
        public ISubscriber Subscriber { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetSubscriber))
            {
                return Subscriber;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class HangingSubscriberProxy : DispatchProxy
    {
        public TaskCompletionSource PublishStarted { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISubscriber.PublishAsync))
            {
                PublishStarted.TrySetResult();
                return new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
