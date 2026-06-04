namespace Observables.Mqtt.R3.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public void Mqtt_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class TemperatureReading
            {
                public double Celsius { get; set; }
            }

            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands/{deviceId}/restart")]
                Observable<Unit> Restart(string deviceId);

                [MqttSubscribe("sensors/+/temperature")]
                Observable<TemperatureReading> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS5002", snapshot, StringComparison.Ordinal);
        Assert.Contains("SensorTopicsGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromPublish", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromSubscribe", snapshot, StringComparison.Ordinal);
        Assert.Contains("MqttTopic.Format", snapshot, StringComparison.Ordinal);
    }
}
