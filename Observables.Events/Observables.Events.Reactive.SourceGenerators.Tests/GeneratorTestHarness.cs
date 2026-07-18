global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Events.Generators;
using Observables.TestSupport;

namespace Observables.Events.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "Observables.Events.Reactive"),
            _ => MetadataReferenceBuilder.Build(typeof(global::System.Reactive.Unit)),
            static () => [new ObservableEventsGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS", thenByDiagnosticMessage: true),
            includeResultDiagnostics: true,
            syntaxTreePath: "/0/Test0.cs"));

    internal static (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview) =>
        Harness.CreateHarnessCompilation(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
            });

    public static GeneratorRunOutput Run(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        IIncrementalGenerator[]? generators = null,
        bool useWpf = false,
        bool observableRoutedEvents = false)
    {
        return Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
                Generators = generators,
                OptionsProvider = new TestAnalyzerConfigOptionsProvider(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["build_property.UseWPF"] = useWpf.ToString(),
                        ["build_property.ObservableRoutedEvents"] = observableRoutedEvents.ToString(),
                    }),
            });
    }

    public static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);
}
