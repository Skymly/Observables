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
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var optionsProvider = context.Options.AnalyzerConfigOptionsProvider;
        foreach (var domain in ProxyDomainCatalog.ReactiveConflictDomains)
        {
            var backends = ReactivePackageConflictDetection.DetectBackends(
                context.Compilation,
                optionsProvider,
                domain.Definition);
            if (backends.Length < 2)
            {
                continue;
            }

            var packageNames = string.Join(
                ", ",
                backends.Select(b => domain.Definition.BackendPackageName(b)));

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConflictingReactivePackages,
                Location.None,
                domain.Definition.DisplayName,
                packageNames));
        }
    }
}
