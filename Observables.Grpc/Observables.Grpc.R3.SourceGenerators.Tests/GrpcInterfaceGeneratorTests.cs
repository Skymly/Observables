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

    [Fact]
    public void Grpc_interface_OBS7003_on_iobservable_with_r3_generator()
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
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7003", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("OBS7005", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Grpc_interface_OBS7002_when_runtime_missing()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains(
            "OBS7002: Observables.Grpc.R3 is not referenced. Add a PackageReference to Observables.Grpc.R3.",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.Grpc is not referenced", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Grpc_interface_OBS7006_on_unary_without_request()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7006", snapshot, StringComparison.Ordinal);
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
    public void Protobuf_empty_still_emits_ForMessage()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<Google.Protobuf.WellKnownTypes.Empty> UnaryEcho(
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
                Observable<int> Next(int n);
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
                Observable<NumbersPoco> Next(NumbersPoco n);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7009", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<NumbersPoco>", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<global::NumbersPoco>", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Unattributed_property_reports_OBS7001()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                Observable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS7001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Grpc_interface_OBS7004_on_unary_property()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS7004", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("OBS7001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_cancellation_token_is_passed_through()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string request, CancellationToken ct);
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
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string request, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Non_trailing_cancellation_token_reports_OBS7001()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(CancellationToken ct, string request);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS7001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Rpc_name_with_quotes_is_escaped()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("a\"b")]
                Observable<string> UnaryEcho(string request);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(@"new(global::Grpc.Core.MethodType.Unary, ""echo.Echo"", ""a\""b""", snapshot, StringComparison.Ordinal);
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

    [Fact]
    public void Public_instance_event_reports_OBS7001()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7001");
    }

    [Fact]
    public void Grpc_interface_OBS7004_on_unary_event()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("Tick")]
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7004");
        Assert.DoesNotContain(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7001");
    }

    [Fact]
    public void Default_service_name_strips_a_single_I_prefix()
    {
        const string userSource =
            """
            [Grpc]
            public interface IInventory
            {
                [GrpcUnary]
                Observable<string> Get(string request);
            }
            """;

        var snapshot = GeneratorTestHarness.ToSnapshot(GeneratorTestHarness.Run(userSource));
        Assert.Contains("\"Inventory\"", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("\"nventory\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_string_uses_the_string_marshaller()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string?> Echo(string? request);
            }
            """;

        var snapshot = GeneratorTestHarness.ToSnapshot(GeneratorTestHarness.Run(userSource));
        Assert.Contains("GrpcMarshallers.String", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<global::System.String?>", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ForMessage<string?>", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_service_name_is_unchanged()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IInventory
            {
                [GrpcUnary]
                Observable<string> Get(string request);
            }
            """;

        var snapshot = GeneratorTestHarness.ToSnapshot(GeneratorTestHarness.Run(userSource));
        Assert.Contains("\"echo.Echo\"", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Inventory\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_boundary_attributes_report_OBS7010()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEcho
            {
                [GrpcUnary("UnaryEcho")]
                [GrpcServerStream("StreamEcho")]
                Observable<string> Echo(string request);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS7010");
    }
}
