using Observables.Redis;
using R3;

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
    public static void Main() => Console.WriteLine("Observables.Redis.R3 consumer smoke OK");
}
