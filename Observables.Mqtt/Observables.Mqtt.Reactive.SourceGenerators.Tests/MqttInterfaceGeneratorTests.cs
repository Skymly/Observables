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

    [Fact]
    public void Client_name_reaches_the_generated_registration()
    {
        const string userSource =
            """
            [Mqtt("primary")]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/+/temperature")]
                IObservable<int> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("RegisterProxyName", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"primary\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Mqtt_interface_without_a_name_emits_no_name_registration()
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
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("RegisterProxyName", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Docs_getting_started_snippet_does_not_report_OBS5006()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/+/temperature")]
                IObservable<double> Temperature { get; }

                [MqttPublish("commands/{deviceId}/restart")]
                IObservable<global::System.Reactive.Unit> Restart(string deviceId);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS5006", snapshot, StringComparison.Ordinal);
        Assert.Contains("SensorTopicsGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromSubscribe", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromPublish", snapshot, StringComparison.Ordinal);
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

    [Fact]
    public void Unattributed_property_reports_OBS5001()
    {
        const string userSource =
            """
            [Mqtt]
            public interface IFeed
            {
                IObservable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS5001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Topic_literal_with_quotes_is_escaped()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("a\"b")]
                IObservable<string> Odd { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(@"FromSubscribe<global::System.String>(_client, ""a\""b"")", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_nullable_cancellation_token_is_not_emitted_as_subscription_ct()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("ping")]
                IObservable<Unit> Ping(CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_instance_event_reports_OBS5001()
    {
        const string userSource =
            """
            [Mqtt]
            public interface IFeed
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5001");
    }

    [Fact]
    public void Mqtt_interface_OBS5002_when_runtime_missing()
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

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains(
            "OBS5002: Observables.Mqtt.Reactive is not referenced. Add a PackageReference to Observables.Mqtt.Reactive.",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.Mqtt is not referenced", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_reactive_adapter_reports_OBS5005()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/temperature")]
                IObservable<int> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5005");
    }

    [Fact]
    public void Mqtt_publish_int_placeholder_converts_to_string()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands/{id}/restart")]
                IObservable<global::System.Reactive.Unit> Restart(int id);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS5", snapshot, StringComparison.Ordinal);
        Assert.Contains(
            @"MqttTopic.Format(""commands/{id}/restart"", (""id"", global::System.Convert.ToString((object?)id, global::System.Globalization.CultureInfo.InvariantCulture)))",
            snapshot,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Mqtt_publish_string_placeholder_keeps_working()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands/{deviceId}/restart")]
                IObservable<global::System.Reactive.Unit> Restart(string deviceId);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS5", snapshot, StringComparison.Ordinal);
        Assert.Contains("MqttTopic.Format", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Mqtt_publish_wildcard_reports_OBS5006()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands/+/restart")]
                IObservable<global::System.Reactive.Unit> Restart();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5006");
    }

    [Fact]
    public void Mqtt_publish_empty_token_reports_OBS5006()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttPublish("commands//restart")]
                IObservable<global::System.Reactive.Unit> Restart();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5006");
    }

    [Fact]
    public void Mqtt_subscribe_hash_not_last_reports_OBS5006()
    {
        const string userSource =
            """
            [Mqtt]
            public interface ISensorTopics
            {
                [MqttSubscribe("sensors/#/temperature")]
                IObservable<string> Temperature { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS5006");
    }
}
