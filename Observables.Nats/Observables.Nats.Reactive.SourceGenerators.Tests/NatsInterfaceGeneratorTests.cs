namespace Observables.Nats.Reactive.SourceGenerators.Tests;

public sealed class NatsInterfaceGeneratorTests
{
    [Fact]
    public void Nats_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.>")]
                IObservable<string> OrderEvents { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS9002", snapshot, StringComparison.Ordinal);
        Assert.Contains("OrderHubGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromSubscribe", snapshot, StringComparison.Ordinal);
    }
}
