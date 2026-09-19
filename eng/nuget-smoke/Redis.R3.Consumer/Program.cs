using System.Reflection;
using Observables.Redis;
using R3;
using StackExchange.Redis;

namespace Observables.NuGetSmoke.Redis.R3;

[Redis]
public interface ISmokeChannels
{
    [RedisSubscribe("ping")]
    Observable<string> Ping { get; }

    [RedisPublish("ping")]
    Observable<Unit> PublishPing(string payload);
}

public static class Program
{
    public static void Main()
    {
        IConnectionMultiplexer multiplexer = DispatchProxy.Create<IConnectionMultiplexer, UnusedRedisProxy>();
        ISmokeChannels hub = RedisService.For<ISmokeChannels>(multiplexer);
        if (hub is null)
        {
            throw new InvalidOperationException("Redis R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Redis.R3 consumer smoke OK");
    }
}

public class UnusedRedisProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
}
