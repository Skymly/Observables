using System.Reflection;
using NATS.Client.Core;
using Observables.Nats;

namespace Observables.NuGetSmoke.Nats.Reactive;

[Nats]
public interface ISmokeSubjects
{
    [NatsSubscribe("ping")]
    IObservable<string> Ping { get; }
}

public static class Program
{
    public static void Main()
    {
        INatsConnection connection = DispatchProxy.Create<INatsConnection, UnusedNatsProxy>();
        ISmokeSubjects hub = NatsService.For<ISmokeSubjects>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Nats Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.Nats.Reactive consumer smoke OK");
    }
}

public class UnusedNatsProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
}
