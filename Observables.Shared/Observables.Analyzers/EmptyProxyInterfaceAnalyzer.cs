using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Observables.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyProxyInterfaceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.EmptyHubInterface,
            DiagnosticDescriptors.EmptyMqttInterface,
            DiagnosticDescriptors.EmptyWebSocketInterface,
            DiagnosticDescriptors.EmptyGrpcInterface,
            DiagnosticDescriptors.EmptySseInterface,
            DiagnosticDescriptors.EmptyNatsInterface,
            DiagnosticDescriptors.EmptyPostgresInterface,
            DiagnosticDescriptors.EmptyRestApiInterface);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeInterface, SymbolKind.NamedType);
    }

    static void AnalyzeInterface(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceSymbol)
        {
            return;
        }

        var domain = ProxyDomainCatalog.TryGetInterfaceProxyDomain(interfaceSymbol, context.Compilation);
        if (domain is null)
        {
            return;
        }

        if (ProxyDomainCatalog.CountPublicInstanceMembers(interfaceSymbol) > 0)
        {
            return;
        }

        var location = interfaceSymbol.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            domain.EmptyInterfaceDescriptor,
            location,
            interfaceSymbol.Name));
    }
}
