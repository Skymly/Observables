namespace Observables.Analyzers.Tests;

public sealed class OpenGenericProxyInterfaceAnalyzerTests
{
    [Fact]
    public void OBS0002_on_generic_mqtt_interface()
    {
        const string source =
            """
            using Observables.Mqtt;
            using R3;

            [Mqtt]
            public interface IFoo<T>
            {
                [MqttSubscribe("x")]
                Observable<T> X { get; }
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            source,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::Observables.Mqtt.MqttAttribute>(),
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
            ],
            new OpenGenericProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS0002" && d.GetMessage().Contains("IFoo", StringComparison.Ordinal));
    }

    [Fact]
    public void No_OBS0002_on_non_generic_mqtt_interface()
    {
        const string source =
            """
            using Observables.Mqtt;
            using R3;

            [Mqtt]
            public interface IFoo
            {
                [MqttSubscribe("x")]
                Observable<int> X { get; }
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            source,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::Observables.Mqtt.MqttAttribute>(),
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
            ],
            new OpenGenericProxyInterfaceAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS0002");
    }
}
