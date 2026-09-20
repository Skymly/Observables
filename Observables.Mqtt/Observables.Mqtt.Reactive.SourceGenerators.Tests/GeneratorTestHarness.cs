global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Mqtt.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Mqtt.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "Observables.Mqtt",
                "MQTTnet.Client"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Mqtt.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Mqtt.MqttService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.Mqtt.Reactive.SystemReactiveMqttAdapter)
                    : null,
                typeof(global::MQTTnet.MqttFactory)),
            static () => [new MqttInterfaceStubGenerator()],
            new SnapshotOptions(
                "OBS5",
                DeduplicateDiagnostics: false,
                WriteNoneForEmptyDiagnostics: false,
                DiagnosticIdComparer: Comparer<string>.Default)));

    static readonly GeneratorHarness HarnessWithoutReactiveAdapter = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "Observables.Mqtt",
                "MQTTnet.Client"),
            _ => MetadataReferenceBuilder.Build(
                "Observables.Mqtt.Reactive.dll",
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.Mqtt.MqttService),
                typeof(global::MQTTnet.MqttFactory)),
            static () => [new MqttInterfaceStubGenerator()],
            new SnapshotOptions(
                "OBS5",
                DeduplicateDiagnostics: false,
                WriteNoneForEmptyDiagnostics: false,
                DiagnosticIdComparer: Comparer<string>.Default)));

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
