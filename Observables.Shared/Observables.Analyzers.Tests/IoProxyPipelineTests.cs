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
    public void Walk_collects_public_events_and_indexers()
    {
        const string source =
            """
            using Observables.Mqtt;
            using System;

            [Mqtt]
            public interface IWeirdHub
            {
                [MqttPublish("x")]
                void Publish(int id);

                event Action Tick;

                string this[int i] { get; }
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);

        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);
        Assert.Single(marked);
        Assert.Contains(marked[0].PublicInstanceMembers, static member => member is IEventSymbol && member.Name == "Tick");
        Assert.Contains(marked[0].PublicInstanceMembers, static member => member is IPropertySymbol { IsIndexer: true });
    }

    [Fact]
    public void Parse_invokes_tryAddOther_for_events()
    {
        const string source =
            """
            using Observables.Mqtt;
            using System;

            [Mqtt]
            public interface IWeirdHub
            {
                event Action Tick;
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);
        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);

        var other = new List<ISymbol>();
        var coreMissing = new DiagnosticDescriptor("TEST000", "c", "{0}", "Test", DiagnosticSeverity.Error, true);
        IoProxyModelAssembly.Parse<string, string, string>(
            marked,
            CancellationToken.None,
            coreReferenced: true,
            coreNotReferenced: coreMissing,
            emptyModel: static () => "",
            tryAddMethod: static (_, _, _, _) => { },
            tryAddProperty: static (_, _, _, _) => { },
            createInterface: static (_, _, _) => "iface",
            createContext: static _ => "ctx",
            tryAddOther: (_, member, _, _) => other.Add(member));

        Assert.Contains(other, static member => member is IEventSymbol && member.Name == "Tick");
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

    [Fact]
    public void ObservableReturnTypeParser_R3_IObservable_is_unsupported_not_missing_reactive()
    {
        var (diagnostics, ok) = ParseObservableReturn(
            """
            using System;
            public interface ISample
            {
                IObservable<int> Go();
            }
            """,
            isR3Generator: true,
            expectedObservableTypeName: null);

        Assert.False(ok);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "TEST001");
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "TEST002");
    }

    [Fact]
    public void ObservableReturnTypeParser_Reactive_IObservable_without_adapter_is_missing_reactive()
    {
        var (diagnostics, ok) = ParseObservableReturn(
            """
            using System;
            public interface ISample
            {
                IObservable<int> Go();
            }
            """,
            isR3Generator: false,
            expectedObservableTypeName: null);

        Assert.False(ok);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "TEST002");
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "TEST001");
    }

    [Fact]
    public void ObservableReturnTypeParser_R3_Observable_succeeds()
    {
        var (diagnostics, ok) = ParseObservableReturn(
            """
            using R3;
            public interface ISample
            {
                Observable<int> Go();
            }
            """,
            isR3Generator: true,
            expectedObservableTypeName: "R3.Observable`1");

        Assert.True(ok);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Walk_collects_inherited_public_instance_members()
    {
        const string source =
            """
            using Observables.Mqtt;

            public interface IBase
            {
                [MqttSubscribe("base")]
                int BaseMember { get; }
            }

            [Mqtt]
            public interface ISub : IBase
            {
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);

        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);
        var sub = Assert.Single(marked, static m => m.InterfaceSymbol.Name == "ISub");
        Assert.Contains(sub.PublicInstanceMembers, static member => member.Name == "BaseMember");
    }

    [Fact]
    public void Walk_dedupes_partial_interface_declarations()
    {
        const string source =
            """
            using Observables.Mqtt;

            [Mqtt]
            public partial interface IFeed
            {
                [MqttPublish("a")]
                void A();
            }

            public partial interface IFeed
            {
                [MqttPublish("b")]
                void B();
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);

        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);
        Assert.Single(marked);
        Assert.Contains(marked[0].PublicInstanceMembers, static member => member.Name == "A");
        Assert.Contains(marked[0].PublicInstanceMembers, static member => member.Name == "B");
    }

    [Fact]
    public void Walk_skips_open_generic_interfaces()
    {
        const string source =
            """
            using Observables.Mqtt;

            [Mqtt]
            public interface IFoo<T>
            {
                [MqttPublish("x")]
                void X();
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var marker = compilation.GetTypeByMetadataName(ProxyDomainTable.Mqtt.InterfaceMarkerMetadataName);
        Assert.NotNull(marker);

        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, marker!, CancellationToken.None);
        Assert.Empty(marked);
    }

    [Fact]
    public void GeneratedProxyClassName_strips_single_leading_I_before_uppercase()
    {
        const string source =
            """
            public interface IInvoice {}
            public interface Invoice {}
            public interface Ifeed {}
            """;

        var (compilation, _) = CompileInterfaces(source);
        Assert.Equal("InvoiceGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(compilation.GetTypeByMetadataName("IInvoice")!));
        Assert.Equal("InvoiceGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(compilation.GetTypeByMetadataName("Invoice")!));
        Assert.Equal("IfeedGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(compilation.GetTypeByMetadataName("Ifeed")!));
    }

    [Fact]
    public void GeneratedHintName_includes_namespace()
    {
        const string source =
            """
            namespace A { public interface ISub {} }
            namespace B { public interface ISub {} }
            """;

        var (compilation, _) = CompileInterfaces(source);
        var a = compilation.GetTypeByMetadataName("A.ISub")!;
        var b = compilation.GetTypeByMetadataName("B.ISub")!;
        Assert.Equal("A_ISub.Mqtt.g.cs", IoProxyModelAssembly.GeneratedHintName(a, "Mqtt"));
        Assert.Equal("B_ISub.Mqtt.g.cs", IoProxyModelAssembly.GeneratedHintName(b, "Mqtt"));
    }

    [Fact]
    public void GeneratedProxyClassName_includes_namespace_containing_type_and_arity()
    {
        const string source =
            """
            namespace A { public interface ISub {} }
            namespace B { public interface ISub {} }
            namespace N { public class Outer { public interface IInner {} } }
            public interface IBox<T> {}
            """;

        var (compilation, _) = CompileInterfaces(source);
        var a = compilation.GetTypeByMetadataName("A.ISub")!;
        var b = compilation.GetTypeByMetadataName("B.ISub")!;
        var inner = compilation.GetTypeByMetadataName("N.Outer+IInner")!;
        var box = compilation.GetTypeByMetadataName("IBox`1")!;

        Assert.Equal("A_SubGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(a));
        Assert.Equal("B_SubGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(b));
        Assert.NotEqual(
            IoProxyModelAssembly.GeneratedProxyClassName(a),
            IoProxyModelAssembly.GeneratedProxyClassName(b));
        Assert.Equal("N_Outer_InnerGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(inner));
        Assert.Equal("Box1GeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(box));
    }

    [Fact]
    public void ExecuteParse_results_are_value_equal_for_equivalent_diagnostics()
    {
        var descriptor = new DiagnosticDescriptor(
            "TEST000",
            "t",
            "m",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        (List<Diagnostic> diagnostics, string model) Parse() =>
            (new List<Diagnostic> { Diagnostic.Create(descriptor, Location.None) }, "model");

        var first = GeneratorFailSafe.ExecuteParse(Parse, descriptor, static () => "");
        var second = GeneratorFailSafe.ExecuteParse(Parse, descriptor, static () => "");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ImmutableEquatableArray_GetHashCode_is_null_safe()
    {
        var array = new ImmutableEquatableArray<string>(new string?[] { null, "x" }!);
        _ = array.GetHashCode();
    }

    static (List<Diagnostic> diagnostics, bool ok) ParseObservableReturn(
        string source,
        bool isR3Generator,
        string? expectedObservableTypeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "ReturnTypeTests",
            [tree],
            AnalyzerTestHarness.GetPlatformReferencesExcludingObservables()
                .Append(AnalyzerTestHarness.CreateReferenceFromAssemblyOf(typeof(R3.Observable<>))),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var method = compilation.GetTypeByMetadataName("ISample")!.GetMembers("Go").OfType<IMethodSymbol>().Single();
        var unsupported = new DiagnosticDescriptor("TEST001", "u", "{0}", "Test", DiagnosticSeverity.Error, true);
        var missingRx = new DiagnosticDescriptor("TEST002", "r", "{0}", "Test", DiagnosticSeverity.Error, true);
        var diagnostics = new List<Diagnostic>();
        var expected = expectedObservableTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(expectedObservableTypeName);

        var ok = ObservableReturnTypeParser.TryParse(
            method.ReturnType,
            compilation,
            reactiveAdapterMetadataName: "Missing.Adapter",
            expectedObservableType: expected,
            unitType: null,
            requiresUnitPayload: false,
            unsupportedReturnType: unsupported,
            systemReactiveNotReferenced: missingRx,
            location: method.Locations[0],
            diagnostics: diagnostics,
            isR3Generator: isR3Generator,
            resultTypeDisplay: out _,
            returnTypeDisplay: out _);

        return (diagnostics, ok);
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

    [Fact]
    public void Walk_hidden_derived_member_wins_over_base()
    {
        const string source =
            """
            using Observables.Mqtt;

            [Mqtt]
            public interface IBaseHub
            {
                [MqttPublish("base/{id}")]
                void Publish(int id);
            }

            [Mqtt]
            public interface IDerivedHub : IBaseHub
            {
                [MqttPublish("derived/{id}")]
                new void Publish(int id);
            }
            """;

        var (compilation, interfaces) = CompileInterfaces(source);
        var mqtt = compilation.GetTypeByMetadataName("Observables.Mqtt.MqttAttribute")!;
        var marked = IoProxyInterfaceWalk.Collect(compilation, interfaces, mqtt, CancellationToken.None);
        var derived = marked.Single(m => m.InterfaceSymbol.Name == "IDerivedHub");
        Assert.Equal(1, derived.PublicInstanceMembers.Count(m => m.Name == "Publish"));
    }
}
