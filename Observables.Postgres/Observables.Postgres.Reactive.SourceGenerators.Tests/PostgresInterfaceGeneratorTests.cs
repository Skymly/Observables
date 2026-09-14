using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Postgres.Reactive.SourceGenerators.Tests;

public sealed class PostgresInterfaceGeneratorTests
{
    [Fact]
    public Task Postgres_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                IObservable<string> OrderCreated { get; }

                [Notify("order_created")]
                IObservable<System.Reactive.Unit> Raise(string payload, CancellationToken cancellationToken = default);
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
                IObservable<string> OrderCreated { get; }

                [Notify("order_created")]
                IObservable<System.Reactive.Unit> Raise(string payload, CancellationToken cancellationToken = default);
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
    public Task Postgres_interface_generates_typed_json_payload_reactive_proxy()
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
                IObservable<OrderPayload> OrderCreated { get; }

                [Notify("order_created")]
                IObservable<System.Reactive.Unit> Raise(OrderPayload payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    const string CacheTestSource =
        """
        [Postgres]
        public interface IOrderChannel
        {
            [Listen("order_created")]
            IObservable<string> OrderCreated { get; }

            [Notify("order_created")]
            IObservable<System.Reactive.Unit> Raise(string payload);
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
                IObservable<string> Ping { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildPostgres");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
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
                IObservable<System.Reactive.Unit> Ping(string payload, CancellationToken? ct);
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
    public void Missing_reactive_adapter_reports_OBS10005()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Listen("orders")]
                IObservable<string> Ping { get; }
            }
            """;

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10005");
    }
}
