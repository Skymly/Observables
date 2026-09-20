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
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.SignalR.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.SignalR.HubService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.SignalR.Reactive.SystemReactiveSignalRAdapter)
                    : null,
                typeof(global::Microsoft.AspNetCore.SignalR.Client.HubConnection)),
            static () => [new HubInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS4")));

    static readonly GeneratorHarness HarnessWithoutReactiveAdapter = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "System.Reactive",
                "Observables.SignalR",
                "Microsoft.AspNetCore.SignalR.Client"),
            _ => MetadataReferenceBuilder.Build(
                "Observables.SignalR.Reactive.dll",
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.SignalR.HubService),
                typeof(global::Microsoft.AspNetCore.SignalR.Client.HubConnection)),
            static () => [new HubInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS4")));

    internal static GeneratorRunOutput Run(
        string userSource,
        bool includeCoreReference = true,
        bool failSafeProbe = false) =>
        Harness.Run(
            userSource,
            new GeneratorHarnessRunOptions
            {
                IncludeCoreReference = includeCoreReference,
                OptionsProvider = failSafeProbe
                    ? new TestAnalyzerConfigOptionsProvider(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["build_property.ObservablesSignalRFailSafeProbe"] = "true",
                        })
                    : null,
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
