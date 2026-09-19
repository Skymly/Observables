using System.Reflection;
using Observables.Redis;
using StackExchange.Redis;

namespace Observables.NuGetSmoke.Redis.Reactive;

[Redis]
public interface ISmokeChannels
{
    [RedisSubscribe("ping")]
    IObservable<string> Ping { get; }
}

public static class Program
{
    public static void Main()
    {
        IConnectionMultiplexer multiplexer = DispatchProxy.Create<IConnectionMultiplexer, UnusedRedisProxy>();
        ISmokeChannels hub = RedisService.For<ISmokeChannels>(multiplexer);
        if (hub is null)
        {
            throw new InvalidOperationException("Redis Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.Redis.Reactive consumer smoke OK");
    }
}

public class UnusedRedisProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
}
