using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Nats.R3.SourceGenerators.Tests;

public sealed class NatsInterfaceGeneratorTests
{
    [Fact]
    public Task Nats_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class OrderEvent
            {
                public string Id { get; set; } = "";
            }

            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders.{id}.cancel")]
                Observable<Unit> Cancel(string id);

                [NatsSubscribe("orders.>")]
                Observable<OrderEvent> OrderEvents { get; }

                [NatsRequest("orders.validate")]
                Observable<string> Validate(string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Nats_interface_OBS9004_on_subscribe_method()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.created")]
                Observable<string> Created();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_interface_OBS9003_on_iobservable_with_r3_generator()
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

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9003", snapshot, StringComparison.Ordinal);
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
                Observable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9002", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_interface_OBS9006_on_subscribe_placeholder()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.{id}")]
                Observable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9006", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        public sealed class OrderEvent
        {
            public string Id { get; set; } = "";
        }

        [Nats]
        public interface IOrderHub
        {
            [NatsPublish("orders.{id}.cancel")]
            Observable<Unit> Cancel(string id);

            [NatsSubscribe("orders.>")]
            Observable<OrderEvent> OrderEvents { get; }

            [NatsRequest("orders.validate")]
            Observable<string> Validate(string payload);
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
                Observable<string> Ping { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildNats");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }


    [Fact]
    public void Unattributed_property_reports_OBS9001()
    {
        const string userSource =
            """
            [Nats]
            public interface IFeed
            {
                Observable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS9001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_cancellation_token_is_passed_through()
    {
        const string userSource =
            """
            [Nats]
            public interface IHub
            {
                [NatsPublish("ping")]
                Observable<Unit> Ping(CancellationToken ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(", ct)", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain(", cancellationToken)", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_trailing_cancellation_token_reports_OBS9001()
    {
        const string userSource =
            """
            [Nats]
            public interface IHub
            {
                [NatsPublish("commands.{id}.restart")]
                Observable<Unit> Restart(CancellationToken ct, string id);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS9001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Subject_literal_with_quotes_is_escaped()
    {
        const string userSource =
            """
            [Nats]
            public interface IHub
            {
                [NatsSubscribe("a\"b")]
                Observable<string> Odd { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(@"FromSubscribe<global::System.String>(_connection, ""a\""b"")", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public Task Nats_interface_with_keyword_parameter_names_generates_valid_code()
    {
        const string userSource =
            """
            [Nats]
            public interface IKeywordHub
            {
                [NatsPublish("orders.{class}.cancel")]
                Observable<Unit> Cancel(string @class);

                [NatsRequest("query.{event}")]
                Observable<string> Query(string @event);
            }
            """;
        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
