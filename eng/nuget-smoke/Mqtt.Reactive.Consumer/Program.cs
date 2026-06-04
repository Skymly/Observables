using Observables.Mqtt;
using System.Reactive;

namespace Observables.NuGetSmoke.Mqtt.Reactive;

[Mqtt]
public interface ISmokeTopics
{
    [MqttSubscribe("sensors/+/temperature")]
    IObservable<int> Temperature { get; }
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Mqtt.Reactive consumer smoke OK");
}
