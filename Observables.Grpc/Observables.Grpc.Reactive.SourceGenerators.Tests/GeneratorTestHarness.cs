global using CacheTrackingDriver = Observables.TestSupport.CacheTrackingDriver;
global using GeneratedSource = Observables.TestSupport.GeneratedSource;
global using GeneratorRunOutput = Observables.TestSupport.GeneratorRunOutput;

using Microsoft.CodeAnalysis;
using Observables.Grpc.Reactive.SourceGenerators;
using Observables.TestSupport;

namespace Observables.Grpc.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    static readonly GeneratorHarness Harness = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "Observables.Grpc"),
            options => MetadataReferenceBuilder.Build(
                options.IncludeCoreReference ? null : "Observables.Grpc.dll",
                typeof(global::System.Reactive.Unit),
                options.IncludeCoreReference
                    ? typeof(global::Observables.Grpc.GrpcService)
                    : null,
                options.IncludeCoreReference
                    ? typeof(global::Observables.Grpc.Reactive.SystemReactiveGrpcAdapter)
                    : null,
                typeof(global::Grpc.Core.CallInvoker),
                typeof(global::Google.Protobuf.WellKnownTypes.Empty)),
            static () => [new GrpcInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS7")));

    static readonly GeneratorHarness HarnessWithoutReactiveAdapter = new(
        new GeneratorHarnessDefinition(
            HarnessDocumentBuilder.Create(
                "System",
                "System.Threading",
                "Observables.Grpc"),
            _ => MetadataReferenceBuilder.Build(
                "Observables.Grpc.Reactive.dll",
                typeof(global::System.Reactive.Unit),
                typeof(global::Observables.Grpc.GrpcService),
                typeof(global::Grpc.Core.CallInvoker),
                typeof(global::Google.Protobuf.WellKnownTypes.Empty)),
            static () => [new GrpcInterfaceStubGenerator()],
            SnapshotOptionsFactory.ForDomain("OBS7")));

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
