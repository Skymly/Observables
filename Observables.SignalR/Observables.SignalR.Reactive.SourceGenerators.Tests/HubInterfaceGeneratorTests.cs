using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.SignalR.Reactive.SourceGenerators.Tests;

public sealed class HubInterfaceGeneratorTests
{
    [Fact]
    public Task Hub_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class ChatMessage
            {
                public string Text { get; set; } = "";
            }

            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                IObservable<int> GetUserCount();

                [HubOn("ReceiveMessage")]
                IObservable<ChatMessage> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Hub_interface_uses_system_reactive_bridge()
    {
        const string userSource =
            """
            [Hub]
            public interface IPingHub
            {
                [HubInvoke]
                IObservable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS4002", snapshot, StringComparison.Ordinal);
        Assert.Contains("SystemReactiveSignalRAdapter", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests (D3-A pilot) ──

    const string CacheTestSource =
        """
        public sealed class ChatMessage
        {
            public string Text { get; set; } = "";
        }

        [Hub]
        public interface IChatHub
        {
            [HubInvoke]
            IObservable<int> GetUserCount();

            [HubOn("ReceiveMessage")]
            IObservable<ChatMessage> ReceiveMessage { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildSignalR step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSignalR");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [Hub] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildSignalR should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSignalR");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_hub_interface_edit_invalidates_build_step()
    {
        // Add a second [Hub] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Hub]
            public interface ISecondHub
            {
                [HubInvoke]
                IObservable<string> Ping();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSignalR");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }
}
