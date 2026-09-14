using Microsoft.CodeAnalysis;
using VerifyXunit;

namespace Observables.Redis.Reactive.SourceGenerators.Tests;

public sealed class RedisInterfaceGeneratorTests
{
    [Fact]
    public Task Redis_interface_generates_reactive_proxy_and_registration()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.{topic}")]
                IObservable<Unit> Publish(string topic, string payload);

                [RedisSubscribe("news.alerts")]
                IObservable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    const string CacheTestSource =
        """
        [Redis]
        public interface INewsHub
        {
            [RedisPublish("news.{topic}")]
            IObservable<Unit> Publish(string topic, string payload);

            [RedisSubscribe("news.alerts")]
            IObservable<string> Alerts { get; }
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
    public Task Redis_pattern_subscribe_and_envelope_modes_generate_reactive_proxy()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.*")]
                IObservable<string> PatternPayload { get; }

                [RedisSubscribe("news.?")]
                IObservable<RedisMessage<string>> PatternEnvelope { get; }

                [RedisSubscribe("news.alerts")]
                IObservable<RedisMessage<string>> ExactEnvelope { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Redis_reactive_publish_int_placeholder_converts_to_string()
    {
        const string userSource =
            """
            [Redis]
            public interface INews
            {
                [RedisPublish("news.{id}")]
                IObservable<Unit> Publish(int id, string payload);
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
    public void Redis_reactive_interface_OBS11004_on_subscribe_method()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.alerts")]
                IObservable<string> Alerts();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_reactive_interface_OBS11006_on_subscribe_placeholder()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisSubscribe("news.{topic}")]
                IObservable<string> Alerts { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11006", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Redis_reactive_interface_OBS11003_on_unsupported_return_type()
    {
        const string userSource =
            """
            [Redis]
            public interface INewsHub
            {
                [RedisPublish("news.alerts")]
                IObservable<string> Publish(string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS11003", snapshot, StringComparison.Ordinal);
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
                IObservable<Unit> Publish(string id, CancellationToken? ct);
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
    public void Missing_reactive_adapter_reports_OBS11005()
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

        var output = GeneratorTestHarness.RunWithoutReactiveAdapter(userSource);
        Assert.Contains(output.Diagnostics, static diagnostic => diagnostic.Id == "OBS11005");
    }
}
