using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Mqtt.R3.SourceGenerators.Tests;

public sealed class MqttInterfaceGeneratorTests
{
    [Fact]
    public Task Mqtt_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class TemperatureReading
            {
                public double Celsius { get; set; }
            }

            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands/{deviceId}/restart")]
                Observable<Unit> Restart(string deviceId);

                [MqttSubscribe("sensors/+/temperature")]
                Observable<TemperatureReading> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Mqtt_interface_OBS5004_on_subscribe_method()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/temperature")]
                Observable<string> Temperature();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS5004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Mqtt_interface_OBS5005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/temperature")]
                IObservable<string> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS5005", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        public sealed class TemperatureReading
        {
            public double Celsius { get; set; }
        }

        [Mqtt]
        public interface ISensorTopics
        {
            [MqttPublish("commands/{deviceId}/restart")]
            Observable<Unit> Restart(string deviceId);

            [MqttSubscribe("sensors/+/temperature")]
            Observable<TemperatureReading> Temperature { get; }
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
                Observable<Unit> Online { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildMqtt");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public Task Mqtt_interface_with_keyword_parameter_names_generates_valid_code()
    {
        const string userSource =
            """
            [Mqtt]
            public interface IKeywordTopics
            {
                [MqttPublish("commands/{class}/restart")]
                Observable<Unit> Restart(string @class);
            }
            """;
        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
