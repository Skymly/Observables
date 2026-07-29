global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Postgres.R3.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Postgres.R3.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "R3",
                "Observables.Postgres",
                "Npgsql"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Postgres.dll",
                typeof(global::R3.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Postgres.PostgresService)
                    : null,
                typeof(global::Npgsql.NpgsqlConnection)),
            static () => [new PostgresInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS10")));

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
