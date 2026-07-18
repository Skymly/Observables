using Microsoft.CodeAnalysis;

namespace Observables.WebSocket.Reactive.SourceGenerators.Tests;

public sealed class BoundaryGeneratorTests
{
    [Fact]
    public void Nested_interface_generates_compilable_proxy()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            public static class HubContainer
            {
                [WebSocket]
                public interface IChatHub
                {
                    [WebSocketReceive]
                    IObservable<string> Messages { get; }
                }
            }
            """);

        Assert.DoesNotContain(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains(
            output.GeneratedSources,
            static source => source.Source.Contains(
                "global::Demo.HubContainer.IChatHub",
                StringComparison.Ordinal));
        Assert.Contains(
            output.GeneratedSources,
            static source => source.Source.Contains(
                "ChatHubGeneratedProxy",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Ref_struct_parameter_generates_compilable_proxy()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            public ref struct Payload
            {
                public int Value;
            }

            [WebSocket]
            public interface IChatHub
            {
                [WebSocketSend]
                IObservable<Unit> Send(Payload payload);
            }
            """);

        Assert.DoesNotContain(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains(
            output.GeneratedSources,
            static source => source.Source.Contains(
                "global::Demo.Payload payload",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Ref_return_method_reports_generated_compilation_error()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            [WebSocket]
            public interface IChatHub
            {
                [WebSocketSend]
                ref IObservable<Unit> Send();
            }
            """);

        Assert.Contains(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
