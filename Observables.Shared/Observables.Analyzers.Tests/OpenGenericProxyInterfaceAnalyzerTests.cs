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
    public void No_OBS0002_for_supported_generic_rest_api()
    {
        const string source = """
            using Observables.RestAPI;

            [RestApi]
            public interface IValues<T>
            {
                [Get("/value")]
                System.Threading.Tasks.Task<T> Get();
            }
            """;
        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            source,
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.RestAPI.RestApiAttribute>()],
            new OpenGenericProxyInterfaceAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id is "OBS0002" or "OBS0003");
    }

    [Fact]
    public void OBS0003_detects_direct_and_inherited_generic_io_methods()
    {
        const string source = """
            using Observables.Nats;
            using Observables.WebSocket;
            using R3;

            [Nats]
            public interface INatsPublisher
            {
                [NatsPublish("echo")]
                Observable<Unit> Publish<T>(T payload);

                T Identity<T>(T value) => value;
            }

            public interface ISharedSender
            {
                [WebSocketSend("echo")]
                Observable<Unit> Send<T>(T payload);
            }

            [WebSocket]
            public interface IWebSocketSender : ISharedSender;
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            source,
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::Observables.Nats.NatsAttribute>(),
                AnalyzerTestHarness.CreateReference<global::Observables.WebSocket.WebSocketAttribute>(),
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
            ],
            new OpenGenericProxyInterfaceAnalyzer());

        var errors = diagnostics.Where(d => d.Id == "OBS0003").Select(d => d.GetMessage()).ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, message => message.Contains("INatsPublisher.Publish<T>", StringComparison.Ordinal));
        Assert.Contains(errors, message => message.Contains("ISharedSender.Send<T>", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, message => message.Contains("Identity", StringComparison.Ordinal));
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
