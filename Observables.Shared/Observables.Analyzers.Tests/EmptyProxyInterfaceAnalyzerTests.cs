using Microsoft.CodeAnalysis.CSharp;

namespace Observables.Analyzers.Tests;

public sealed class EmptyProxyInterfaceAnalyzerTests
{
    [Fact]
    public void OBS4007_on_empty_hub_interface()
    {
        const string source =
            """
            using Observables.SignalR;

            [Hub]
            public interface IChatHub
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.SignalR"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.SignalR.HubAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS4007");
    }

    [Fact]
    public void No_OBS4007_when_hub_interface_has_members()
    {
        const string source =
            """
            using Observables.SignalR;
            using R3;

            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                Observable<int> GetCount();
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.SignalR", "R3"),
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::Observables.SignalR.HubAttribute>(),
                AnalyzerTestHarness.CreateReference<global::R3.Unit>(),
            ],
            new EmptyProxyInterfaceAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS4007");
    }

    [Fact]
    public void OBS5007_on_empty_mqtt_interface()
    {
        const string source =
            """
            using Observables.Mqtt;

            [Mqtt]
            public interface ITopics
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Mqtt"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Mqtt.MqttAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS5007");
    }

    [Fact]
    public void OBS6007_on_empty_websocket_interface()
    {
        const string source =
            """
            using Observables.WebSocket;

            [WebSocket]
            public interface IStream
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.WebSocket"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.WebSocket.WebSocketAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS6007");
    }

    [Fact]
    public void OBS9007_on_empty_nats_interface()
    {
        const string source =
            """
            using Observables.Nats;

            [Nats]
            public interface ISubjects
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Nats"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Nats.NatsAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS9007");
    }

    static string BuildSource(string body, params string[] usings)
    {
        var usingLines = string.Join('\n', usings.Select(u => $"using {u};"));
        return $$"""
            {{usingLines}}

            namespace Test;

            {{body}}
            """;
    }
}
