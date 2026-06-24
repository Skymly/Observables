using VerifyXunit;

namespace Observables.Nats.Reactive.SourceGenerators.Tests;

public sealed class NatsInterfaceGeneratorTests
{
    [Fact]
    public Task Nats_interface_generates_reactive_proxy()
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
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }
}
