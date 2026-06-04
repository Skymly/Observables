namespace Observables.Mqtt.Reactive.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public void Mqtt_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/+/temperature")]
                IObservable<int> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS5002", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromSubscribe", snapshot, StringComparison.Ordinal);
        Assert.Contains("System.IObservable", snapshot, StringComparison.Ordinal);
    }
}
