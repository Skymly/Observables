using VerifyXunit;

namespace Observables.Grpc.R3.SourceGenerators.Tests;

public sealed class GrpcInterfaceGeneratorTests
{
    [Fact]
    public Task Grpc_interface_generates_proxy_and_registration()
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
        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
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
