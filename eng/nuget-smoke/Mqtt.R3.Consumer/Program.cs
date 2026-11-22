#if MQTTNET5
using Observables.Mqtt.MQTTnet5;
using ClientFactory = MQTTnet.MqttClientFactory;
#else
using Observables.Mqtt.MQTTnet4;
using ClientFactory = MQTTnet.MqttFactory;
#endif
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
        using var client = new ClientFactory().CreateMqttClient();
        ISmokeTopics hub = MqttService.For<ISmokeTopics>(MqttNetTransport.Wrap(client));
        if (hub is null)
        {
            throw new InvalidOperationException("Mqtt R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.Mqtt.R3 consumer smoke OK");
    }
}
