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
    public void OBS7007_on_empty_grpc_interface()
    {
        const string source =
            """
            using Observables.Grpc;

            [Grpc]
            public interface IGreeter
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Grpc"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Grpc.GrpcAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS7007");
    }

    [Fact]
    public void OBS8007_on_empty_sse_interface()
    {
        const string source =
            """
            using Observables.Sse;

            [Sse]
            public interface IEvents
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Sse"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Sse.SseAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS8007");
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

    [Fact]
    public void OBS10007_on_empty_postgres_interface()
    {
        const string source =
            """
            using Observables.Postgres;

            [Postgres]
            public interface IChannels
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Postgres"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Postgres.PostgresAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS10007");
    }

    [Fact]
    public void OBS11007_on_empty_redis_interface()
    {
        const string source =
            """
            using Observables.Redis;

            [Redis]
            public interface IChannels
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.Redis"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.Redis.RedisAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS11007");
    }

    [Fact]
    public void ProxyDomainCatalog_Redis_lists_publish_and_subscribe_suggestions()
    {
        var domain = ProxyDomainCatalog.Redis;
        Assert.Equal("OBS11007", domain.EmptyInterfaceDescriptor.Id);
        Assert.Equal("Observables.Redis.RedisAttribute", domain.InterfaceMarkerMetadataName);
        Assert.Contains(domain.MethodAttributes, s => s.DisplayText == "RedisPublish");
        Assert.Contains(domain.PropertyAttributes, s => s.DisplayText == "RedisSubscribe");
    }

    [Fact]
    public void OBS3007_on_empty_restapi_interface()
    {
        const string source =
            """
            using Observables.RestAPI;

            [RestApi]
            public interface IEmptyApi
            {
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.RestAPI"),
            additionalReferences: [AnalyzerTestHarness.CreateReference<global::Observables.RestAPI.RestApiAttribute>()],
            new EmptyProxyInterfaceAnalyzer());

        Assert.Contains(diagnostics, d => d.Id == "OBS3007");
    }

    [Fact]
    public void No_OBS3007_when_restapi_interface_has_members()
    {
        const string source =
            """
            using Observables.RestAPI;
            using System.Threading.Tasks;

            [RestApi]
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<string> GetUser(int id);
            }
            """;

        var diagnostics = AnalyzerTestHarness.RunAnalyzers(
            BuildSource(source, "Observables.RestAPI", "System.Threading.Tasks"),
            additionalReferences:
            [
                AnalyzerTestHarness.CreateReference<global::Observables.RestAPI.RestApiAttribute>(),
                AnalyzerTestHarness.CreateReference<global::Observables.RestAPI.GetAttribute>(),
            ],
            new EmptyProxyInterfaceAnalyzer());

        Assert.DoesNotContain(diagnostics, d => d.Id == "OBS3007");
    }

    [Fact]
    public void ProxyDomainCatalog_RestApi_uses_own_empty_interface_descriptor()
    {
        // Regression: RestApi used to incorrectly reuse EmptyHubInterface (OBS4007).
        var domain = ProxyDomainCatalog.RestApi;
        Assert.Equal("OBS3007", domain.EmptyInterfaceDescriptor.Id);
        Assert.Equal("Empty RestAPI proxy interface", domain.EmptyInterfaceDescriptor.Title);
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
