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
    static readonly string[] SharedUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
        "Observables.RestAPI",
    ];

    static readonly SnapshotOptions SnapshotOptions =
        SnapshotOptionsFactory.ForDomain("OBS3", thenByDiagnosticMessage: true);

    static readonly GeneratorHarness R3Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create([.. SharedUsings, "R3"]),
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
            SnapshotOptions,
            includeResultDiagnostics: true));

    static readonly GeneratorHarness ReactiveHarness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(SharedUsings),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.RestAPI.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.RestAPI.RestService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter)
                    : null),
            static () => [new Observables.RestAPI.Reactive.SourceGenerators.RestApiInterfaceStubGenerator()],
            SnapshotOptions,
            includeResultDiagnostics: true));

    internal static (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        MetadataReference[]? extraReferences = null,
        bool includeCoreReference = true)
        => R3Harness.CreateHarnessCompilation(
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
        => R3Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
                Generators = generators,
                ExtraReferences = extraReferences,
                IncludeCoreReference = includeCoreReference,
            });

    public static GeneratorRunOutput RunReactive(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        IIncrementalGenerator[]? generators = null,
        MetadataReference[]? extraReferences = null,
        bool includeCoreReference = true)
        => ReactiveHarness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                LanguageVersion = languageVersion,
                Generators = generators,
                ExtraReferences = extraReferences,
                IncludeCoreReference = includeCoreReference,
            });

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        R3Harness.RunWithCacheTracking(userSource);

    internal static CacheTrackingDriver RunWithCacheTrackingReactive(string userSource) =>
        ReactiveHarness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        R3Harness.GetStepReason(result, stepName);

    public static string ToSnapshot(GeneratorRunOutput output) =>
        R3Harness.ToSnapshot(output);
}
