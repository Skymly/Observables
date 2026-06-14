using Observables.Nats;
using R3;

namespace Observables.NuGetSmoke.Nats.R3;

[Nats]
public interface ISmokeSubjects
{
    [NatsPublish("ping")]
    Observable<Unit> Ping();
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Nats.R3 consumer smoke OK");
}
