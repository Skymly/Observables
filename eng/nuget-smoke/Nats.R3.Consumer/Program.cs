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
    public static void Main() => Console.WriteLine("Observables.Nats.R3 consumer smoke OK");
}
