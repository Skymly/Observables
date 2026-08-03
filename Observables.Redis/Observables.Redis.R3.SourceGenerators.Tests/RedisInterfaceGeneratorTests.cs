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
}
