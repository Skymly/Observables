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
    public void Postgres_interface_OBS10005_on_iobservable_with_r3_generator()
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

        Assert.Contains("OBS10005", snapshot, StringComparison.Ordinal);
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
}
