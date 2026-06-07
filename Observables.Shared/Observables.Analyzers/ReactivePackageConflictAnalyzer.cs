using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Observables.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReactivePackageConflictAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ConflictingReactivePackages);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        if (!ReactivePackageConflictDetection.HasR3Reference(context.Compilation))
        {
            return;
        }

        foreach (var domain in ProxyDomainCatalog.ReactiveConflictDomains)
        {
            if (!ReactivePackageConflictDetection.HasReactiveBridgeReference(context.Compilation, domain))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConflictingReactivePackages,
                Location.None,
                domain.DisplayName));
        }
    }
}
