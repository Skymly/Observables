global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Postgres.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Postgres.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "Observables.Postgres",
                "Npgsql"),
            _ => MetadataReferenceBuilder.Build(
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.Postgres.PostgresService),
                typeof(global::Observables.Postgres.Reactive.SystemReactivePostgresAdapter),
                typeof(global::Npgsql.NpgsqlConnection)),
            static () => [new PostgresInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS10")));

    internal static GeneratorRunOutput Run(string userSource) =>
        Harness.Run(userSource);

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    internal static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
