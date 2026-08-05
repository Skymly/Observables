global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Redis.R3.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Redis.R3.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "R3",
                "Observables.Redis",
                "StackExchange.Redis"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Redis.dll",
                typeof(global::R3.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Redis.RedisService)
                    : null,
                typeof(global::StackExchange.Redis.ConnectionMultiplexer)),
            static () => [new RedisInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS11")));

    internal static GeneratorRunOutput Run(string userSource, bool includeCoreReference = true) =>
        Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                IncludeCoreReference = includeCoreReference,
            });

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    internal static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
