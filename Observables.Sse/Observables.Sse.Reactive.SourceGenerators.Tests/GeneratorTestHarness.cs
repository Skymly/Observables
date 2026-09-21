global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Sse.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Sse.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "System.Reactive",
                "Observables.Sse"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Sse.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Sse.SseService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.Sse.Reactive.SystemReactiveSseAdapter)
                    : null),
            static () => [new SseInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS8")));

    static readonly GeneratorHarness HarnessWithoutReactiveAdapter = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "System.Reactive",
                "Observables.Sse"),
            _ => MetadataReferenceBuilder.Build(
                "Observables.Sse.Reactive.dll",
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.Sse.SseService)),
            static () => [new SseInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS8")));

    internal static GeneratorRunOutput Run(string userSource, bool includeCoreReference = true) =>
        Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                IncludeCoreReference = includeCoreReference,
            });

    internal static GeneratorRunOutput RunWithoutReactiveAdapter(string userSource) =>
        HarnessWithoutReactiveAdapter.Run(userSource);

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    internal static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
