namespace Observables.Grpc.R3.SourceGenerators.Tests;

public sealed class GrpcInterfaceGeneratorTests
{
    [Fact]
    public void Grpc_interface_generates_proxy_and_registration()
    {
        const string userSource =
            """
            [Grpc("echo.Echo")]
            public interface IEchoService
            {
                [GrpcUnary("UnaryEcho")]
                Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);

                [GrpcServerStream("StreamEcho")]
                Observable<string> StreamEcho(string request, CancellationToken cancellationToken = default);

                [GrpcClientStream("Collect")]
                Observable<string> Collect(Observable<string> requests, CancellationToken cancellationToken = default);

                [GrpcDuplex("Chat")]
                Observable<string> Chat(Observable<string> requests, CancellationToken cancellationToken = default);
            }
            """;

        var output = GeneratorTestHarness.Run(userSource);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("OBS7002", snapshot, StringComparison.Ordinal);
        Assert.Contains("EchoServiceGeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.Contains("RegisterGeneratedFactory", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromUnary", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromServerStreaming", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromClientStreaming", snapshot, StringComparison.Ordinal);
        Assert.Contains("FromDuplexStreaming", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_without_Grpc_attribute_produces_no_output()
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

        Assert.DoesNotContain("GeneratedProxy", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("OBS7", snapshot, StringComparison.Ordinal);
    }
}
