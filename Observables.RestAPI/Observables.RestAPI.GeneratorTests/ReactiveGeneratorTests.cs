using Microsoft.CodeAnalysis;
using Observables.TestSupport;

namespace Observables.RestAPI.GeneratorTests;

public class ReactiveGeneratorTests
{
    [Fact]
    public Task GetIObservableUser_uses_system_reactive_from_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.RunReactive(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                IObservable<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public Task R3_Observable_on_Reactive_generator_reports_OBS3003()
    {
        GeneratorRunOutput output = GeneratorTestHarness.RunReactive(
            """
            using R3;

            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """,
            extraReferences: MetadataReferenceBuilder.Build(typeof(global::R3.Unit)));

        Assert.Contains("OBS3003", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    // ── Incremental cache hit tests (Reactive generator) ──

    const string CacheTestSource =
        """
        public interface IUserApi
        {
            [Get("/users/{id}")]
            Task<User> GetUser(int id);
        }

        public sealed class User
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTrackingReactive(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTrackingReactive(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Cached
                or IncrementalStepRunReason.Unchanged
                or IncrementalStepRunReason.Modified,
            $"Expected cache hit or Modified (Cached/Unchanged/Modified), got {reason}");
    }

    [Fact]
    public void Cache_restapi_interface_edit_invalidates_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTrackingReactive(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            public interface ISecondApi
            {
                [Get("/ping")]
                Task<string> Ping();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }
}
