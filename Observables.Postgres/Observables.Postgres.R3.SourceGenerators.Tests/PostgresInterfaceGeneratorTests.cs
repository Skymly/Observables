using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Postgres.R3.SourceGenerators.Tests;

public sealed class PostgresInterfaceGeneratorTests
{
    [Fact]
    public Task Postgres_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                Observable<string> OrderCreated { get; }

                [Notify("order_created")]
                Observable<Unit> Raise(string payload, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Generated_proxy_carries_json_trim_warnings()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                Observable<string> OrderCreated { get; }

                [Notify("order_created")]
                Observable<Unit> Raise(string payload, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(
            output.GeneratedSources,
            static s => s.HintName.Contains("IOrderChannel.Postgres.g.cs", StringComparison.Ordinal)
                && s.Source.Contains("RequiresUnreferencedCode(\"JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.\")", StringComparison.Ordinal)
                && s.Source.Contains("RequiresDynamicCode(\"JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.\")", StringComparison.Ordinal));
    }

    [Fact]
    public Task Postgres_interface_generates_typed_json_payload_proxy()
    {
        const string userSource =
            """
            public sealed class OrderPayload
            {
                public string OrderId { get; set; } = "";
                public int Quantity { get; set; }
            }

            [Postgres]
            public interface ITypedOrderChannel
            {
                [Listen("order_created")]
                Observable<OrderPayload> OrderCreated { get; }

                [Notify("order_created")]
                Observable<Unit> Raise(OrderPayload payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Postgres_interface_OBS10004_on_listen_method()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                Observable<string> Created();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS10004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Postgres_interface_OBS10003_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                IObservable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS10003", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Postgres_interface_OBS10001_on_non_literal_channel()
    {
        const string userSource =
            """
            public static class Channels
            {
                public const string Order = "order_created";
            }

            [Postgres]
            public interface IOrderChannel
            {
                [Listen(Channels.Order)]
                Observable<string> OrderCreated { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS10001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Postgres_interface_OBS10002_when_runtime_missing()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                Observable<string> OrderCreated { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS10002", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Postgres_interface_OBS10006_on_invalid_channel()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order-created")]
                Observable<string> OrderCreated { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS10006", snapshot, StringComparison.Ordinal);
    }

    const string CacheTestSource =
        """
        [Postgres]
        public interface IOrderChannel
        {
            [Listen("order_created")]
            Observable<string> OrderCreated { get; }

            [Notify("order_created")]
            Observable<Unit> Raise(string payload);
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildPostgres");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildPostgres");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_postgres_interface_edit_invalidates_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Postgres]
            public interface ISecondChannel
            {
                [Listen("orders")]
                Observable<string> Ping { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildPostgres");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public void Unattributed_property_reports_OBS10001()
    {
        const string userSource =
            """
            [Postgres]
            public interface IFeed
            {
                Observable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS10001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_cancellation_token_is_passed_through()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Notify("ping")]
                Observable<Unit> Ping(CancellationToken ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("ct)", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("cancellationToken)", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_nullable_cancellation_token_is_not_emitted_as_subscription_ct()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Notify("ping")]
                Observable<Unit> Ping(string payload, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Non_trailing_cancellation_token_reports_OBS10001()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Notify("ping")]
                Observable<Unit> Ping(CancellationToken ct, string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS10001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_instance_event_reports_OBS10001()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10001");
    }

    [Fact]
    public void Real_listen_attribute_is_preferred_over_simple_name_twin()
    {
        const string userSource =
            """
            namespace Other
            {
                public sealed class ListenAttribute : System.Attribute
                {
                    public ListenAttribute(string? channel = null) { }
                }
            }

            [Postgres]
            public interface IHub
            {
                [Listen("real_channel")]
                [Other.Listen("fake_channel")]
                Observable<string> Orders { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS10", snapshot, StringComparison.Ordinal);
        Assert.Contains("real_channel", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("fake_channel", snapshot, StringComparison.Ordinal);
    }
}
