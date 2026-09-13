using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Grpc.Reactive.SourceGenerators.Tests;

public sealed class GrpcInterfaceGeneratorTests
{
    [Fact]
    public Task Grpc_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                IObservable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    // ── Incremental cache hit tests (D3-A pilot) ──

    const string CacheTestSource =
        """
        [Grpc("echo.Echo")]
        public interface IEchoService
        {
            [GrpcUnary("UnaryEcho")]
            IObservable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);

            [GrpcServerStream("StreamEcho")]
            IObservable<string> StreamEcho(string request, CancellationToken cancellationToken = default);
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildGrpc step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildGrpc");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no [Grpc] interfaces).
        // ForAttributeWithMetadataName filters at the syntax level, so the
        // candidate set is unchanged → BuildGrpc should cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildGrpc");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_grpc_interface_edit_invalidates_build_step()
    {
        // Add a second [Grpc] interface → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            [Grpc("ping.Ping")]
            public interface IPingService
            {
                [GrpcUnary("Ping")]
                IObservable<string> Ping(string request, CancellationToken cancellationToken = default);
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildGrpc");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public void Protobuf_empty_still_emits_ForMessage()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                IObservable<Google.Protobuf.WellKnownTypes.Empty> UnaryEcho(
                    Google.Protobuf.WellKnownTypes.Empty request);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS7009", snapshot, StringComparison.Ordinal);
        Assert.Contains("ForMessage<global::Google.Protobuf.WellKnownTypes.Empty>()", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Int_request_and_response_report_OBS7009_without_ForMessage()
    {
        const string userSource =
            """
            [Grpc]
            public interface INumbers
            {
                [GrpcUnary]
                IObservable<int> Next(int n);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7009", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<int>", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<global::System.Int32>", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Poco_request_and_response_report_OBS7009_without_ForMessage()
    {
        const string userSource =
            """
            public sealed class NumbersPoco
            {
                public int N { get; set; }
            }

            [Grpc]
            public interface INumbers
            {
                [GrpcUnary]
                IObservable<NumbersPoco> Next(NumbersPoco n);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7009", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<NumbersPoco>", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<global::NumbersPoco>", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_instance_event_reports_OBS7001()
    {
        const string userSource =
            """
            [Grpc]
            public interface IEcho
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7001");
    }
}
