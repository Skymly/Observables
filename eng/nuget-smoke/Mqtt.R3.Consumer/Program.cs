using Observables.Mqtt;
using R3;

namespace Observables.NuGetSmoke.Mqtt.R3;

[Mqtt]
public interface ISmokeTopics
{
    [MqttPublish("ping")]
    Observable<Unit> Ping();
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Mqtt.R3 consumer smoke OK");
}
