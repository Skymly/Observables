using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.SignalR.R3.SourceGenerators.Tests;

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
                Observable<int> GetUserCount();

                [HubOn("ReceiveMessage")]
                Observable<ChatMessage> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
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
                Observable<int> GetUserCount();
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
                Observable<int> GetUserCount();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("RegisterProxyName", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_interface_OBS4004_on_hub_on_method()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubOn("ReceiveMessage")]
                Observable<string> ReceiveMessage();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_interface_OBS4003_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Hub]
            public interface IChatHub
            {
                [HubOn("ReceiveMessage")]
                IObservable<string> ReceiveMessage { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4003", snapshot, StringComparison.Ordinal);
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
                Observable<int> GetUserCount();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4002", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_interface_OBS4006_on_streaming_parameter()
    {
        const string userSource =
            """
            using System.Collections.Generic;

            [Hub]
            public interface IChatHub
            {
                [HubInvoke]
                Observable<int> Stream(IAsyncEnumerable<int> items);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS4006", snapshot, StringComparison.Ordinal);
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
            Observable<int> GetUserCount();

            [HubOn("ReceiveMessage")]
            Observable<ChatMessage> ReceiveMessage { get; }
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
                Observable<string> Ping();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildSignalR");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }


    [Fact]
    public void Unattributed_property_reports_OBS4001()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                Observable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS4001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_cancellation_token_is_passed_through()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubInvoke]
                Observable<string> Echo(string msg, CancellationToken ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(", ct)", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain(", cancellationToken)", snapshot, StringComparison.Ordinal);
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
                Observable<string> Echo(string msg, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.DoesNotContain(output.Diagnostics, static diagnostic => diagnostic.Id == "CS1503");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Non_trailing_cancellation_token_reports_OBS4001()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubInvoke]
                Observable<string> Echo(CancellationToken ct, string msg);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS4001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Hub_method_name_with_quotes_is_escaped()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubOn("a\"b")]
                Observable<string> Odd { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(@"FromOn<global::System.String>(_connection, ""a\""b"")", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public Task Hub_interface_with_keyword_parameter_names_generates_valid_code()
    {
        const string userSource =
            """
            [Hub]
            public interface IKeywordHub
            {
                [HubInvoke]
                Observable<int> GetCount(int @class, string @event);
            }
            """;
        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
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
    public void Multiple_boundary_attributes_report_OBS4009()
    {
        const string userSource =
            """
            [Hub]
            public interface IChat
            {
                [HubInvoke]
                [HubSend]
                Observable<int> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS4009");
    }
}
