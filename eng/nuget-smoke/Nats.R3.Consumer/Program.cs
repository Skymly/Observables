using System.Reflection;
using NATS.Client.Core;
using Observables.Nats;
using R3;

namespace Observables.NuGetSmoke.Nats.R3;

[Nats]
public interface ISmokeSubjects
{
    [NatsPublish("ping.{name}")]
    Observable<Unit> Ping(string name);
}

public static class Program
{
    public static void Main()
    {
        INatsConnection connection = DispatchProxy.Create<INatsConnection, UnusedNatsProxy>();
        ISmokeSubjects hub = NatsService.For<ISmokeSubjects>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("Nats R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Nats.R3 consumer smoke OK");
    }
}

public class UnusedNatsProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException(targetMethod?.Name);
}
