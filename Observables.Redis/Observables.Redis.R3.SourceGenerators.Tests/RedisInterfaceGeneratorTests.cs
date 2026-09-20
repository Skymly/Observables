using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Redis.R3.SourceGenerators.Tests;

public sealed class RedisInterfaceGeneratorTests
{
    [Fact]
    public Task Redis_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.{topic}")]
                Observable<Unit> Publish(string topic, string payload);

                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Connection_name_reaches_the_generated_registration()
    {
        const string userSource =
            """
            [Redis("primary")]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("RegisterProxyName", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"primary\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_without_a_name_emits_no_name_registration()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("RegisterProxyName", snapshot, StringComparison.Ordinal);
    }

    const string CacheTestSource =
        """
        [Redis]
        public interface INewsHub
        {
            [RedisPublish("news.{topic}")]
            Observable<Unit> Publish(string topic, string payload);

            [RedisSubscribe("news.alerts")]
            Observable<string> Alerts { get; }
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRedis");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public Task Redis_pattern_subscribe_and_envelope_modes_generate_proxy()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.*")]
                Observable<string> PatternPayload { get; }

                [RedisSubscribe("news.?")]
                Observable<RedisMessage<string>> PatternEnvelope { get; }

                [RedisSubscribe("news.alerts")]
                Observable<RedisMessage<string>> ExactEnvelope { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Redis_interface_OBS11001_on_unannotated_member()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                Observable<Unit> Publish(string topic, string payload);

                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11001_on_non_literal_channel()
    {
        const string userSource =
            """
            public static class Channels
            {
                public const string Alerts = "news.alerts";
            }

            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe(Channels.Alerts)]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11002_when_runtime_missing()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource, includeCoreReference: false);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11002", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11003_on_unsupported_return_type()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.alerts")]
                Observable<string> Publish(string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11003", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11004_on_subscribe_method()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                Observable<string> Alerts();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11004_on_publish_property()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.alerts")]
                Observable<Unit> Publish { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11003_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                IObservable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11003", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_publish_int_placeholder_converts_to_string()
    {
        const string userSource =
            """
            [Redis]
            public interface INews
            {
                [RedisPublish("news.{id}")]
                Observable<Unit> Publish(int id, string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS11", snapshot, StringComparison.Ordinal);
        Assert.Contains(
            @"RedisChannelTemplate.Format(""news.{id}"", (""id"", global::System.Convert.ToString((object?)id, global::System.Globalization.CultureInfo.InvariantCulture)))",
            snapshot,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11006_on_subscribe_placeholder()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.{topic}")]
                Observable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11006", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_interface_OBS11006_on_publish_pattern_metacharacters()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.*")]
                Observable<Unit> Publish(string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11006", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Unattributed_property_reports_OBS11001()
    {
        const string userSource =
            """
            [Redis]
            public interface IFeed
            {
                Observable<int> Bare { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS11001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Renamed_cancellation_token_is_passed_through()
    {
        const string userSource =
            """
            [Redis]
            public interface IHub
            {
                [RedisPublish("ping")]
                Observable<Unit> Ping(CancellationToken ct);
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
            [Redis]
            public interface IHub
            {
                [RedisPublish("ping")]
                Observable<Unit> Publish(string id, CancellationToken? ct);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS11006");
        foreach (var source in output.GeneratedSources)
        {
            Assert.DoesNotContain("ct = default", source.Source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Non_trailing_cancellation_token_reports_OBS11001()
    {
        const string userSource =
            """
            [Redis]
            public interface IHub
            {
                [RedisPublish("commands/{id}")]
                Observable<Unit> Publish(CancellationToken ct, string id);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS11001", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Channel_literal_with_quotes_is_escaped()
    {
        const string userSource =
            """
            [Redis]
            public interface IHub
            {
                [RedisSubscribe("a\"b")]
                Observable<string> Odd { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(@"FromSubscribe<global::System.String>(_multiplexer, ""a\""b"")", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Public_instance_event_reports_OBS11001()
    {
        const string userSource =
            """
            [Redis]
            public interface IHub
            {
                event System.Action Tick;
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS11001");
    }

    [Fact]
    public void Redis_character_class_stays_literal_subscribe_without_explicit_pattern()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.[ab]")]
                Observable<string> CharacterClass { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS11", snapshot, StringComparison.Ordinal);
        Assert.Contains(@"FromSubscribe<global::System.String>(_multiplexer, ""news.[ab]"")", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("FromPatternSubscribe", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_explicit_pattern_maps_character_class_to_psubscribe()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.[ab]", Pattern = true)]
                Observable<string> CharacterClass { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS11", snapshot, StringComparison.Ordinal);
        Assert.Contains(@"FromPatternSubscribe<global::System.String>(_multiplexer, ""news.[ab]"")", snapshot, StringComparison.Ordinal);
    }
}
