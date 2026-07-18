global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.SignalR.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.SignalR.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "System.Reactive",
                "Observables.SignalR",
                "Microsoft.AspNetCore.SignalR.Client"),
            _ => MetadataReferenceBuilder.Build(
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.SignalR.HubService),
                typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter),
                typeof(global::Microsoft.AspNetCore.SignalR.Client.HubConnection)),
            static () => [new HubInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS4")));

    internal static GeneratorRunOutput Run(string userSource) =>
        Harness.Run(userSource);

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    internal static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
