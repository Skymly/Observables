using Microsoft.CodeAnalysis;
using Observables.TestSupport;
using VerifyXunit;

namespace Observables.Postgres.Reactive.SourceGenerators.Tests;

public sealed class PostgresInterfaceGeneratorTests
{
    [Fact]
    public void Postgres_interface_OBS10002_when_runtime_missing()
    {
        const string userSource =
            """
            [Postgres]
            public interface IOrderChannel
            {
                [Listen("order_created")]
                IObservable<string> OrderCreated { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains(
            "OBS10002: Observables.Postgres.Reactive is not referenced. Add a PackageReference to Observables.Postgres.Reactive.",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.Postgres is not referenced", snapshot, StringComparison.Ordinal);
    }

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

    [Fact]
    public void Generated_proxy_carries_json_trim_warnings()
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
        Assert.Contains(
            output.GeneratedSources,
            static s => s.HintName.Contains("IOrderChannel.Postgres.g.cs", StringComparison.Ordinal)
                && s.Source.Contains("RequiresUnreferencedCode(\"JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.\")", StringComparison.Ordinal)
                && s.Source.Contains("RequiresDynamicCode(\"JSON payload serialization uses System.Text.Json reflection. Preserve payload type members when trimming.\")", StringComparison.Ordinal));
    }

    [Fact]
    public Task Postgres_interface_generates_typed_json_payload_reactive_proxy()
    {
        const string userSource =
            """
            public sealed class OrderPayload
            {
                public string OrderId { get; set; } = "";
                public int Quantity { get; set; }
            }

            [Postgres]
            public interface ITypedOrderChannel
            {
                [Listen("order_created")]
                IObservable<OrderPayload> OrderCreated { get; }

                [Notify("order_created")]
                IObservable<System.Reactive.Unit> Raise(OrderPayload payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Theory]
    [InlineData(@"wire_pg\n")]
    [InlineData(@"wire_pg\r\n")]
    public void Postgres_rejects_channels_with_trailing_line_breaks(string channelSource)
    {
        var output = GeneratorTestHarness.Run(
            $$"""
              [Postgres]
              public interface IChannel
              {
                  [Listen("{{channelSource}}")]
                  IObservable<string> Read { get; }

                  [Notify("{{channelSource}}")]
                  IObservable<global::System.Reactive.Unit> Send(string payload);
              }
              """);

        Assert.Equal(2, output.Diagnostics.Where(static diagnostic => diagnostic.Id == "OBS10006")
            .Select(static diagnostic => diagnostic.GetMessage()).Distinct().Count());
    }

    [Theory]
    [InlineData("wire_pg", true)]
    [InlineData(@"wire_pg\n", false)]
    public void Postgres_reads_and_validates_channels_in_inherited_contract_metadata(
        string listenChannelSource,
        bool validListenChannel)
    {
        var contracts =
            $$"""
            using System;
            using System.Reactive;
            using Observables.Postgres;

            public interface IExternalContract
            {
                [Listen("{{listenChannelSource}}")]
                IObservable<string> Changes { get; }

                [Notify(channel: "wire_notify")]
                IObservable<Unit> Send(string payload);

                [Listen]
                IObservable<string> DefaultChannel { get; }
            }
            """;
        var (compilation, _) = GeneratorTestRunner.CreateHarnessCompilation(
            contracts,
            static source => source,
            MetadataReferenceBuilder.Build(typeof(ListenAttribute), typeof(global::System.Reactive.Unit)));

        using var assembly = new MemoryStream();
        var emit = compilation.WithAssemblyName("PostgresContracts")
            .Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var output = GeneratorTestHarness.Run(
            "[Postgres] public interface IChannel : IExternalContract { }",
            extraReferences: [MetadataReference.CreateFromImage(assembly.ToArray())]);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        if (!validListenChannel)
        {
            Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10006"
                && diagnostic.GetMessage().Contains("IExternalContract.Changes", StringComparison.Ordinal));
            Assert.DoesNotContain("FromListen(_connection, \"Changes\")", snapshot, StringComparison.Ordinal);
            return;
        }

        Assert.DoesNotContain(output.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains("FromListen(_connection, \"wire_pg\")", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromNotify(_connection, \"wire_notify\", payload, default)", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromListen(_connection, \"DefaultChannel\")", snapshot, StringComparison.Ordinal);
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

    [Fact]
    public void Trailing_nullable_cancellation_token_is_not_emitted_as_subscription_ct()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Notify("ping")]
                IObservable<System.Reactive.Unit> Ping(string payload, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Public_instance_event_reports_OBS10001()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10001");
    }

    [Fact]
    public void Missing_reactive_adapter_reports_OBS10005()
    {
        const string userSource =
            """
            [Postgres]
            public interface IHub
            {
                [Listen("orders")]
                IObservable<string> Ping { get; }
            }
            """;

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS10005");
    }

    [Fact]
    public void Real_listen_attribute_is_preferred_over_simple_name_twin()
    {
        const string userSource =
            """
            namespace Other
            {
                public sealed class ListenAttribute : System.Attribute
                {
                    public ListenAttribute(string? channel = null) { }
                }
            }

            [Postgres]
            public interface IHub
            {
                [Listen("real_channel")]
                [Other.Listen("fake_channel")]
                IObservable<string> Orders { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS10", snapshot, StringComparison.Ordinal);
        Assert.Contains("real_channel", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("fake_channel", snapshot, StringComparison.Ordinal);
    }
}
