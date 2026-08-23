using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.Roslyn.Shared;
using Observables.SourceGenerators.Shared;
using Observables.SourceGenerators.Shared.Diagnostics;

namespace Observables.Analyzers.Tests;

public sealed class IoProxyPipelineTests
{
    [Fact]
    public void Catalog_markers_cover_the_eight_IO_stub_domains()
    {
        Assert.Equal("Observables.SignalR.HubAttribute", ProxyDomainTable.SignalR.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Mqtt.MqttAttribute", ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.WebSocket.WebSocketAttribute", ProxyDomainTable.WebSocket.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Grpc.GrpcAttribute", ProxyDomainTable.Grpc.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Sse.SseAttribute", ProxyDomainTable.Sse.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Nats.NatsAttribute", ProxyDomainTable.Nats.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Postgres.PostgresAttribute", ProxyDomainTable.Postgres.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.Redis.RedisAttribute", ProxyDomainTable.Redis.InterfaceMarkerMetadataName);
        Assert.Equal("Observables.RestAPI.RestApiAttribute", ProxyDomainTable.RestApi.InterfaceMarkerMetadataName);
        Assert.Equal(8, ProxyDomainTable.MemberBoundaryDomains.Count);
    }

    [Fact]
    public void Walk_collects_marked_public_members_and_skips_unmarked()
    {
        const string source =
            """
            using Observables.Mqtt;

            [Mqtt]
            public interface IWeatherHub
            {
                [MqttPublish("sensors/{id}")]
                void Publish(int id);

                private static int Hidden() => 0;
            }

            public interface INotMqtt
            {
                void Ignored();
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);

        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);
        Assert.Single(marked);
        Assert.Equal("IWeatherHub", marked[0].InterfaceSymbol.Name);
        Assert.Contains(marked[0].PublicInstanceMembers, static member => member.Name == "Publish");
        Assert.DoesNotContain(marked[0].PublicInstanceMembers, static member => member.Name == "Hidden");
    }

    [Fact]
    public void OBS7004_stays_on_the_Grpc_adapter()
    {
        // Shared does not template DiagnosticDescriptors. OBS7004 is the Grpc
        // member-shape id in the catalog; reporting it belongs in the Grpc
        // adapter once a concrete mismatch is classified there.
        Assert.Equal("OBS7004", ProxyDomainTable.Grpc.MemberShapeMismatchDiagnosticId);
    }

    [Fact]
    public void ObservableReturnTypeParser_rejects_bare_task()
    {
        const string source =
            """
            using System.Threading.Tasks;
            public interface ISample
            {
                Task<int> Go();
            }
            """;

        var (compilation, _) = CompileInterfaces(source);
        var method = compilation.GetTypeByMetadataName("ISample")!.GetMembers("Go").OfType<IMethodSymbol>().Single();
        var unsupported = new DiagnosticDescriptor("TEST001", "u", "{0}", "Test", DiagnosticSeverity.Error, true);
        var missingRx = new DiagnosticDescriptor("TEST002", "r", "{0}", "Test", DiagnosticSeverity.Error, true);
        var diagnostics = new List<Diagnostic>();

        var ok = ObservableReturnTypeParser.TryParse(
            method.ReturnType,
            compilation,
            reactiveAdapterMetadataName: "Missing.Adapter",
            expectedObservableType: null,
            unitType: null,
            requiresUnitPayload: false,
            unsupportedReturnType: unsupported,
            systemReactiveNotReferenced: missingRx,
            location: method.Locations[0],
            diagnostics: diagnostics,
            resultTypeDisplay: out _,
            returnTypeDisplay: out _);

        Assert.False(ok);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "TEST001");
    }

    static (CSharpCompilation compilation, ImmutableArray<InterfaceDeclarationSyntax> interfaces) CompileInterfaces(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "WalkTests",
            [tree],
            AnalyzerTestHarness.GetPlatformReferencesExcludingObservables()
                .Append(AnalyzerTestHarness.CreateReference<global::Observables.Mqtt.MqttAttribute>()),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var interfaces = tree.GetRoot()
            .DescendantNodes()
            .OfType<InterfaceDeclarationSyntax>()
            .ToImmutableArray();
        return (compilation, interfaces);
    }
}
