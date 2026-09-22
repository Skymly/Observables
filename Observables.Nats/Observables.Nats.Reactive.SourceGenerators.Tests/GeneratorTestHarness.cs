global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Nats.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Nats.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "Observables.Nats",
                "NATS.Client.Core"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Nats.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Nats.NatsService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.Nats.Reactive.SystemReactiveNatsAdapter)
                    : null,
                typeof(global::NATS.Client.Core.NatsConnection)),
            static () => [new NatsInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS9")));

    static readonly GeneratorHarness HarnessWithoutReactiveAdapter = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "Observables.Nats",
                "NATS.Client.Core"),
            _ => MetadataReferenceBuilder.Build(
                "Observables.Nats.Reactive.dll",
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.Nats.NatsService),
                typeof(global::NATS.Client.Core.NatsConnection)),
            static () => [new NatsInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS9")));

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
