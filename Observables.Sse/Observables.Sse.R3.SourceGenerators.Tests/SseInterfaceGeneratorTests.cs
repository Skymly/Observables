using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Sse.R3.SourceGenerators.Tests;

public sealed class SseInterfaceGeneratorTests
{
    [Fact]
    public Task Sse_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Sse]
            public interface IPriceFeed
            {
                [SseEvent("price")]
                Observable<string> Prices { get; }

                [SseEvent]
                Observable<string> Heartbeats { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Interface_without_Sse_attribute_produces_no_output()
    {
        const string userSource =
            """
            public interface IPlain
            {
                string Foo { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS8", snapshot, StringComparison.Ordinal);
        Assert.Empty(output.GeneratedSources);
    }

    [Fact]
    public void Sse_interface_OBS8001_on_unannotated_method()
    {
        const string userSource =
            """
            [Sse]
            public interface IBadFeed
            {
                Observable<string> NoAttribute();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Sse_interface_OBS8004_on_event_method()
    {
        const string userSource =
            """
            [Sse]
            public interface IFeed
            {
                [SseEvent("price")]
                Observable<string> Prices();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Sse_interface_OBS8005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Sse]
            public interface IFeed
            {
                [SseEvent("price")]
                IObservable<string> Prices { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8005", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests (D3-A pilot) ──

    const string CacheTestSource =
        """
        [Sse]
        public interface IPriceFeed
        {
            [SseEvent("price")]
            Observable<string> Prices { get; }

            [SseEvent]
            Observable<string> Heartbeats { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildSse step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSse");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [Sse] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildSse should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSse");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_sse_interface_edit_invalidates_build_step()
    {
        // Add a second [Sse] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Sse]
            public interface ISecondFeed
            {
                [SseEvent("ping")]
                Observable<string> Ping { get; }
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSse");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public Task Sse_interface_with_keyword_property_name_generates_valid_code()
    {
        const string userSource =
            """
            [Sse]
            public interface IKeywordFeed
            {
                [SseEvent("update")]
                Observable<string> @event { get; }
            }
            """;
        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
