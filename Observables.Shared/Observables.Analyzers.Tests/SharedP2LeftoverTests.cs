using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Roslyn.Shared;
using Observables.SourceGenerators.Shared;

namespace Observables.Analyzers.Tests;

public sealed class SharedP2LeftoverTests
{
    [Fact]
    public void Reactive_conflict_catalog_includes_Events_without_adding_it_to_interface_proxies()
    {
        Assert.Contains(ProxyDomainCatalog.ReactiveConflictDomains, static d => d.Definition.DisplayName == "Events");
        Assert.DoesNotContain(ProxyDomainCatalog.InterfaceProxyDomains, static d => d.Definition.DisplayName == "Events");
        Assert.Equal(
            ProxyDomainTable.ConsumerPackageConflictDomains.Select(static d => d.DisplayName),
            ProxyDomainCatalog.ReactiveConflictDomains.Select(static d => d.Definition.DisplayName));
        Assert.Equal("Observables.Events.Reactive", ProxyDomainTable.Events.ReactivePackageName);
        Assert.Equal("Observables.Events.R3", ProxyDomainTable.Events.R3PackageName);
    }

    [Fact]
    public void OBS0002_on_nested_interface_inside_open_generic()
    {
        const string source =
            """
            using Observables.Mqtt;
            using R3;

            public class Outer<T>
            {
                [Mqtt]
                public interface IInner
                {
                    [MqttSubscribe("x")]
                    Observable<int> X { get; }
                }
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

        Assert.Contains(diagnostics, d => d.Id == "OBS0002" && d.GetMessage().Contains("IInner", StringComparison.Ordinal));
    }

    [Fact]
    public void NullableContext_Enabled_with_ContextInherited_is_treated_as_enabled()
    {
        var context = NullableContext.Enabled | NullableContext.ContextInherited;
        Assert.True(context.HasFlag(NullableContext.Enabled));
        Assert.NotEqual(NullableContext.Enabled, context);
    }

    [Fact]
    public void IInvoice_and_Invoice_keep_distinct_proxy_class_names_when_disambiguated()
    {
        var (invoice, iInvoice) = GetInvoiceSymbols();
        Assert.Equal(
            IoProxyModelAssembly.GeneratedProxyClassName(invoice),
            IoProxyModelAssembly.GeneratedProxyClassName(iInvoice));
        Assert.NotEqual(
            IoProxyModelAssembly.GeneratedProxyClassName(invoice, keepInterfacePrefix: true),
            IoProxyModelAssembly.GeneratedProxyClassName(iInvoice, keepInterfacePrefix: true));
        Assert.Equal("InvoiceGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(invoice, keepInterfacePrefix: true));
        Assert.Equal("IInvoiceGeneratedProxy", IoProxyModelAssembly.GeneratedProxyClassName(iInvoice, keepInterfacePrefix: true));
    }

    static (INamedTypeSymbol Invoice, INamedTypeSymbol IInvoice) GetInvoiceSymbols()
    {
        var syntax = CSharpSyntaxTree.ParseText("public interface IInvoice {} public interface Invoice {}");
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES missing.");
        MetadataReference[] references = tpa
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
        var compilation = CSharpCompilation.Create("invoices", [syntax], references);
        return (
            compilation.GetTypeByMetadataName("Invoice")!,
            compilation.GetTypeByMetadataName("IInvoice")!);
    }
}
