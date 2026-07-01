using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Mqtt.Reactive.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public Task Mqtt_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/+/temperature")]
                IObservable<int> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        [Mqtt]
        public interface ISensorTopics
        {
            [MqttSubscribe("sensors/+/temperature")]
            IObservable<int> Temperature { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildMqtt step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildMqtt");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [Mqtt] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildMqtt should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildMqtt");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_mqtt_interface_edit_invalidates_build_step()
    {
        // Add a second [Mqtt] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Mqtt]
            public interface ISecondTopics
            {
                [MqttSubscribe("status/online")]
                IObservable<Unit> Online { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildMqtt");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }
}
