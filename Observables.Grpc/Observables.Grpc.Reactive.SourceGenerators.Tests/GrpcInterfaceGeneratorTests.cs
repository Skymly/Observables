namespace Observables.Grpc.Reactive.SourceGenerators.Tests;

public sealed class GrpcInterfaceGeneratorTests
{
    [Fact]
    public void Grpc_interface_generates_reactive_proxy()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                IObservable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS7005", snapshot, StringComparison.Ordinal);
        Assert.Contains("EchoServiceGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("SystemReactiveGrpcAdapter", snapshot, StringComparison.Ordinal);
    }
}
