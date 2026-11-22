#if MQTTNET5
using Observables.Mqtt.MQTTnet5;
using ClientFactory = MQTTnet.MqttClientFactory;
#else
using Observables.Mqtt.MQTTnet4;
using ClientFactory = MQTTnet.MqttFactory;
#endif
using Observables.Mqtt;

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
        using var client = new ClientFactory().CreateMqttClient();
        ISmokeTopics hub = MqttService.For<ISmokeTopics>(MqttNetTransport.Wrap(client));
        if (hub.Temperature is null)
        {
            throw new InvalidOperationException("Mqtt Reactive generated proxy did not create the temperature observable.");
        }

        Console.WriteLine("Observables.Mqtt.Reactive consumer smoke OK");
    }
}
