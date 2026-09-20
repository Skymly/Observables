using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Observables.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OpenGenericProxyInterfaceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.OpenGenericProxyInterface);

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

        if (!IsOpenGeneric(interfaceSymbol))
        {
            return;
        }

        var domain = ProxyDomainCatalog.TryGetInterfaceProxyDomain(interfaceSymbol, context.Compilation);
        if (domain is null)
        {
            return;
        }

        var location = interfaceSymbol.Locations.FirstOrDefault() ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.OpenGenericProxyInterface,
            location,
            interfaceSymbol.Name));
    }

    internal static bool IsOpenGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
