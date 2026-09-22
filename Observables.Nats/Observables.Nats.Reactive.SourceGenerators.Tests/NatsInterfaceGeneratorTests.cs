using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Nats.Reactive.SourceGenerators.Tests;

public sealed class NatsInterfaceGeneratorTests
{
    [Fact]
    public Task Nats_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> OrderEvents { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Connection_name_reaches_the_generated_registration()
    {
        const string userSource =
            """
            [Nats("primary")]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> OrderEvents { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("RegisterProxyName", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"primary\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_interface_without_a_name_emits_no_name_registration()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> OrderEvents { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("RegisterProxyName", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        [Nats]
        public interface IOrderHub
        {
            [NatsSubscribe("orders.>")]
            IObservable<string> OrderEvents { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildNats step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildNats");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [Nats] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildNats should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildNats");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_nats_interface_edit_invalidates_build_step()
    {
        // Add a second [Nats] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Nats]
            public interface ISecondHub
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> Ping { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildNats");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public void Trailing_nullable_cancellation_token_is_not_emitted_as_subscription_ct()
    {
        const string userSource =
            """
            [Nats]
            public interface IHub
            {
                [NatsPublish("ping")]
                IObservable<Unit> Restart(string id, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_instance_event_reports_OBS9001()
    {
        const string userSource =
            """
            [Nats]
            public interface IFeed
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9001");
    }

    [Fact]
    public void Nats_interface_OBS9002_when_runtime_missing()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.created")]
                IObservable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains(
            "OBS9002: Observables.Nats.Reactive is not referenced. Add a PackageReference to Observables.Nats.Reactive.",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.Nats is not referenced", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_reactive_adapter_reports_OBS9005()
    {
        const string userSource =
            """
            [Nats]
            public interface IFeed
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> Ping { get; }
            }
            """;

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9005");
    }

    [Fact]
    public void Nats_publish_int_placeholder_converts_to_string()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders.{id}.cancel")]
                IObservable<global::System.Reactive.Unit> Cancel(int id);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS9", snapshot, StringComparison.Ordinal);
        Assert.Contains(
            @"NatsSubject.Format(""orders.{id}.cancel"", (""id"", global::System.Convert.ToString((object?)id, global::System.Globalization.CultureInfo.InvariantCulture)))",
            snapshot,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_publish_string_placeholder_keeps_working()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders.{id}.cancel")]
                IObservable<global::System.Reactive.Unit> Cancel(string id);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS9", snapshot, StringComparison.Ordinal);
        Assert.Contains("NatsSubject.Format", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_publish_wildcard_reports_OBS9006()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders.*.cancel")]
                IObservable<global::System.Reactive.Unit> Cancel();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9006");
    }

    [Fact]
    public void Nats_publish_empty_token_reports_OBS9006()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders..cancel")]
                IObservable<global::System.Reactive.Unit> Cancel();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9006");
    }

    [Fact]
    public void Nats_subscribe_gt_not_last_reports_OBS9006()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.>.created")]
                IObservable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS9006");
    }
}
