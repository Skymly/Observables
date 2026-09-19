using MQTTnet;
using MQTTnet.Client;
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
    public static void Main()
    {
        IMqttClient client = new MqttFactory().CreateMqttClient();
        ISmokeTopics hub = MqttService.For<ISmokeTopics>(client);
        if (hub is null)
        {
            throw new InvalidOperationException("Mqtt Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.Mqtt.Reactive consumer smoke OK");
    }
}
