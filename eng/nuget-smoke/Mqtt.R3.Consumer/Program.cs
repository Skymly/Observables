using MQTTnet;
using MQTTnet.Client;
using Observables.Mqtt;
using R3;

namespace Observables.NuGetSmoke.Mqtt.R3;

[Mqtt]
public interface ISmokeTopics
{
    [MqttPublish("ping.{name}")]
    Observable<Unit> Ping(string name);
}

public static class Program
{
    public static void Main()
    {
        IMqttClient client = new MqttFactory().CreateMqttClient();
        ISmokeTopics hub = MqttService.For<ISmokeTopics>(client);
        if (hub is null)
        {
            throw new InvalidOperationException("Mqtt R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Mqtt.R3 consumer smoke OK");
    }
}
