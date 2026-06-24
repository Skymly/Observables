using VerifyXunit;

namespace Observables.Sse.Reactive.SourceGenerators.Tests;

public sealed class SseInterfaceGeneratorTests
{
    [Fact]
    public Task Sse_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Sse]
            public interface IPriceFeed
            {
                [SseEvent("price")]
                IObservable<string> Prices { get; }

                [SseEvent]
                IObservable<string> Heartbeats { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Sse_interface_OBS8004_on_event_method()
    {
        const string userSource =
            """
            [Sse]
            public interface IFeed
            {
                [SseEvent("price")]
                IObservable<string> Prices();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8004", snapshot, StringComparison.Ordinal);
    }
}
