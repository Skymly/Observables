using VerifyXunit;

namespace Observables.Mqtt.R3.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public Task Mqtt_interface_generates_proxy_and_registration()
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
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Mqtt_interface_OBS5004_on_subscribe_method()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/temperature")]
                Observable<string> Temperature();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS5004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Mqtt_interface_OBS5005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/temperature")]
                IObservable<string> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS5005", snapshot, StringComparison.Ordinal);
    }
}
