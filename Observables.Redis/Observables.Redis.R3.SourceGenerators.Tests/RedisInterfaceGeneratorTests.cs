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
    public void Redis_interface_OBS11005_on_iobservable_with_r3_generator()
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

        Assert.Contains("OBS11005", snapshot, StringComparison.Ordinal);
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
}
