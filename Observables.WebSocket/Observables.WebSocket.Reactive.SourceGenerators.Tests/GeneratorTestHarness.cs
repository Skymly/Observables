global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.TestSupport;
using Observables.WebSocket.Reactive.SourceGenerators;

namespace Observables.WebSocket.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Net.WebSockets",
                "System.Threading",
                "System.Reactive",
                "Observables.WebSocket"),
            _ => MetadataReferenceBuilder.Build(
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.WebSocket.WebSocketService),
                typeof(global::Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter)),
            static () => [new WebSocketInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS6")));

    internal static GeneratorRunOutput Run(string userSource) =>
        Harness.Run(userSource);

    internal static CacheTrackingDriver RunWithCacheTracking(string userSource) =>
        Harness.RunWithCacheTracking(userSource);

    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName) =>
        Harness.GetStepReason(result, stepName);

    internal static string ToSnapshot(GeneratorRunOutput output) =>
        Harness.ToSnapshot(output);
}
