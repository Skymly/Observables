using VerifyXunit;

namespace Observables.Nats.R3.SourceGenerators.Tests;

public sealed class NatsInterfaceGeneratorTests
{
    [Fact]
    public Task Nats_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            public sealed class OrderEvent
            {
                public string Id { get; set; } = "";
            }

            [Nats]
            public interface IOrderHub
            {
                [NatsPublish("orders.{id}.cancel")]
                Observable<Unit> Cancel(string id);

                [NatsSubscribe("orders.>")]
                Observable<OrderEvent> OrderEvents { get; }

                [NatsRequest("orders.validate")]
                Observable<string> Validate(string payload);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Nats_interface_OBS9004_on_subscribe_method()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.created")]
                Observable<string> Created();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Nats_interface_OBS9005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Nats]
            public interface IOrderHub
            {
                [NatsSubscribe("orders.created")]
                IObservable<string> Created { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS9005", snapshot, StringComparison.Ordinal);
    }
}
