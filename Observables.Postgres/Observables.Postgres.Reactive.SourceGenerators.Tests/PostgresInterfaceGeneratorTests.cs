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
}
