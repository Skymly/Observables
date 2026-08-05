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
}
