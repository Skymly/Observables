using Observables.Redis;

namespace Observables.NuGetSmoke.Redis.Reactive;

[Redis]
public interface ISmokeChannels
{
    [RedisSubscribe("ping")]
    IObservable<string> Ping { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Redis.Reactive consumer smoke OK");
}
