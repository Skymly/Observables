namespace Observables.Sse.R3.SourceGenerators.Tests;

public sealed class SseInterfaceGeneratorTests
{
    [Fact]
    public void Sse_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Sse]
            public interface IPriceFeed
            {
                [SseEvent("price")]
                Observable<string> Prices { get; }

                [SseEvent]
                Observable<string> Heartbeats { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS8002", snapshot, StringComparison.Ordinal);
        Assert.Contains("PriceFeedGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromEvent", snapshot, StringComparison.Ordinal);
        // explicit event name routed through
        Assert.Contains("\"price\"", snapshot, StringComparison.Ordinal);
        // default event name for parameterless [SseEvent]
        Assert.Contains("\"message\"", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_without_Sse_attribute_produces_no_output()
    {
        const string userSource =
            """
            public interface IPlain
            {
                string Foo { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS8", snapshot, StringComparison.Ordinal);
        Assert.Empty(output.GeneratedSources);
    }

    [Fact]
    public void Sse_interface_OBS8001_on_unannotated_method()
    {
        const string userSource =
            """
            [Sse]
            public interface IBadFeed
            {
                Observable<string> NoAttribute();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8001", snapshot, StringComparison.Ordinal);
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
                Observable<string> Prices();
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8004", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Sse_interface_OBS8005_on_iobservable_with_r3_generator()
    {
        const string userSource =
            """
            [Sse]
            public interface IFeed
            {
                [SseEvent("price")]
                IObservable<string> Prices { get; }
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS8005", snapshot, StringComparison.Ordinal);
    }
}
