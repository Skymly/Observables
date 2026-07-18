global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.RestAPI.R3.SourceGenerators;
using Observables.TestSupport;

namespace Observables.RestAPI.GeneratorTests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Collections.Generic",
                "System.Net.Http",
                "System.Threading",
                "System.Threading.Tasks",
                "R3",
                "Observables.RestAPI"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.RestAPI.dll",
                typeof(global::R3.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.RestAPI.RestService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter)
                    : null),
            static () => [new RestApiR3InterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS3", thenByDiagnosticMessage: true),
            includeResultDiagnostics: true));

    internal static (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        MetadataReference[]? extraReferences = null,
        bool includeCoreReference = true)
        => Harness.CreateHarnessCompilation(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
                ExtraReferences = extraReferences,
                IncludeCoreReference = includeCoreReference,
            });

    public static GeneratorRunOutput Run(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        IIncrementalGenerator[]? generators = null,
        MetadataReference[]? extraReferences = null,
        bool includeCoreReference = true)
        => Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
                Generators = generators,
                ExtraReferences = extraReferences,
                IncludeCoreReference = includeCoreReference,
            });

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    public static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
