using VerifyXunit;

namespace Observables.Mqtt.Reactive.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public Task Mqtt_interface_generates_reactive_proxy()
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
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
