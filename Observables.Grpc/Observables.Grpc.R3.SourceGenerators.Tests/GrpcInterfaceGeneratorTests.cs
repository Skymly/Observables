using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Grpc.R3.SourceGenerators.Tests;

public sealed class GrpcInterfaceGeneratorTests
{
    [Fact]
    public Task Grpc_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);

                [GrpcServerStream("StreamEcho")]
                Observable<string> StreamEcho(string request, CancellationToken cancellationToken = default);

                [GrpcClientStream("Collect")]
                Observable<string> Collect(Observable<string> requests, CancellationToken cancellationToken = default);

                [GrpcDuplex("Chat")]
                Observable<string> Chat(Observable<string> requests, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Interface_without_Grpc_attribute_produces_no_output()
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

        Assert.DoesNotContain("GeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("OBS7", snapshot, StringComparison.Ordinal);
    }

    // ── Incremental cache hit tests (D3-A pilot) ──

    const string CacheTestSource =
        """
        [Grpc("echo.Echo")]
        public interface IEchoService
        {
            [GrpcUnary("UnaryEcho")]
            Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);

            [GrpcServerStream("StreamEcho")]
            Observable<string> StreamEcho(string request, CancellationToken cancellationToken = default);
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
                Observable<string> Ping(string request, CancellationToken cancellationToken = default);
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildGrpc");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }

    [Fact]
    public Task Grpc_interface_with_keyword_parameter_names_generates_valid_code()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IKeywordService
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string @event, CancellationToken cancellationToken = default);
            }
            """;
        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
