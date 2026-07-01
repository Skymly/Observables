using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.WebSocket.R3.SourceGenerators.Tests;

public sealed class WebSocketInterfaceGeneratorTests
{
    [Fact]
    public Task WebSocket_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [WebSocket]
            public interface IMyHub
            {
                [WebSocketConnect]
                Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

                [WebSocketClose]
                Observable<Unit> Close(CancellationToken cancellationToken = default);

                [WebSocketSend("ping")]
                Observable<Unit> Ping(CancellationToken cancellationToken = default);

                [WebSocketReceive("message")]
                Observable<string> Messages { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void WebSocket_interface_string_send_generates_FromSendText()
    {
        // string parameter must produce a Text frame (FromSendText), not Binary (FromSend).
        const string userSource =
            """
            [WebSocket]
            public interface IChatHub
            {
                [WebSocketSend]
                Observable<Unit> SendMessage(string message);

                [WebSocketReceive]
                Observable<string> Incoming { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS6", snapshot, StringComparison.Ordinal);
        Assert.Contains("ChatHubGeneratedProxy", snapshot, StringComparison.Ordinal);
        // string → FromSendText (Text frame)
        Assert.Contains("FromSendText", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromReceive", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_without_WebSocket_attribute_produces_no_output()
    {
        // An interface without [WebSocket] is simply ignored by the generator; no diagnostics, no generated source.
        const string userSource =
            """
            public interface IPlain
            {
                string Foo { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS6", snapshot, StringComparison.Ordinal);
        Assert.Empty(output.GeneratedSources);
    }

    [Fact]
    public void WebSocket_interface_OBS6001_on_unannotated_member()
    {
        // [WebSocket] interface but one method has no boundary attribute → OBS6001
        const string userSource =
            """
            [WebSocket]
            public interface IBadHub
            {
                Observable<string> NoAttribute();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS6001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSocket_interface_OBS6004_on_receive_method()
    {
        const string userSource =
            """
            [WebSocket]
            public interface IHub
            {
                [WebSocketReceive("ping")]
                Observable<string> Ping();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS6004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSocket_interface_OBS6005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [WebSocket]
            public interface IHub
            {
                [WebSocketReceive("ping")]
                IObservable<string> Ping { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS6005", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        [WebSocket]
        public interface IMyHub
        {
            [WebSocketConnect]
            Observable<Unit> Connect(Uri uri, CancellationToken cancellationToken = default);

            [WebSocketClose]
            Observable<Unit> Close(CancellationToken cancellationToken = default);

            [WebSocketSend("ping")]
            Observable<Unit> Ping(CancellationToken cancellationToken = default);

            [WebSocketReceive("message")]
            Observable<string> Messages { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildWebSocket step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildWebSocket");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [WebSocket] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildWebSocket should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildWebSocket");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_websocket_interface_edit_invalidates_build_step()
    {
        // Add a second [WebSocket] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [WebSocket]
            public interface ISecondHub
            {
                [WebSocketSend]
                Observable<Unit> Ping(CancellationToken cancellationToken = default);
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildWebSocket");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }
}
