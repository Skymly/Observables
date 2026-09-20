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

    [Fact]
    public void Hub_interface_OBS4002_when_runtime_missing()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                IObservable<int> GetUserCount();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains(
            "OBS4002: Observables.SignalR.Reactive is not referenced. Add a PackageReference to Observables.SignalR.Reactive.",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.SignalR is not referenced", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_name_reaches_the_generated_registration()
    {
        const string userSource =
            """
            [Hub("primary")]
            public interface IChatHub
            {
                [HubInvoke]
                IObservable<int> GetUserCount();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("RegisterProxyName", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"primary\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_without_a_name_emits_no_name_registration()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                IObservable<int> GetUserCount();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("RegisterProxyName", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Trailing_nullable_cancellation_token_is_not_emitted_as_subscription_ct()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubInvoke]
                IObservable<string> Echo(string msg, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.DoesNotContain(output.Diagnostics, static diagnostic => diagnostic.Id == "CS1503");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
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

    [Fact]
    public void Public_instance_event_reports_OBS4001()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS4001");
    }

    [Fact]
    public void Missing_reactive_adapter_reports_OBS4005()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubOn("ReceiveMessage")]
                IObservable<string> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS4005");
    }

    [Fact]
    public void Multiple_boundary_attributes_report_OBS4009()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubInvoke]
                [HubSend]
                IObservable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS4009");
    }
}
