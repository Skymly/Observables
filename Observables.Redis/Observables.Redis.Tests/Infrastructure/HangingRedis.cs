using System.Reflection;
using StackExchange.Redis;

namespace Observables.Redis.Tests.Infrastructure;

internal static class HangingRedis
{
    public static HangingPublish CreateForPublish()
    {
        var publishStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = DispatchProxy.Create<ISubscriber, HangingSubscriberProxy>();
        var hanging = (HangingSubscriberProxy)(object)subscriber;
        hanging.PublishStarted = publishStarted;
        hanging.InFlight = inFlight;

        var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, HangingMultiplexerProxy>();
        ((HangingMultiplexerProxy)(object)multiplexer).Subscriber = subscriber;
        return new HangingPublish(multiplexer, publishStarted.Task, inFlight.Task, hanging);
    }

    public sealed class HangingPublish
    {
        readonly HangingSubscriberProxy _subscriber;

        internal HangingPublish(
            IConnectionMultiplexer multiplexer,
            Task publishStarted,
            Task inFlight,
            HangingSubscriberProxy subscriber)
        {
            Multiplexer = multiplexer;
            PublishStarted = publishStarted;
            InFlight = inFlight;
            _subscriber = subscriber;
        }

        public IConnectionMultiplexer Multiplexer { get; }
        public Task PublishStarted { get; }
        public Task InFlight { get; }
        public object?[]? LastPublishArgs => _subscriber.LastPublishArgs;
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
        public TaskCompletionSource<long> InFlight { get; set; } = null!;
        public object?[]? LastPublishArgs { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISubscriber.PublishAsync))
            {
                LastPublishArgs = args;
                PublishStarted.TrySetResult();
                return InFlight.Task;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public static IConnectionMultiplexer CreateThrowingSubscribe()
    {
        var subscriber = DispatchProxy.Create<ISubscriber, ThrowingSubscriberProxy>();
        var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, HangingMultiplexerProxy>();
        ((HangingMultiplexerProxy)(object)multiplexer).Subscriber = subscriber;
        return multiplexer;
    }

    public class ThrowingSubscriberProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ISubscriber.SubscribeAsync))
            {
                throw new InvalidOperationException("subscribe-failed");
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
