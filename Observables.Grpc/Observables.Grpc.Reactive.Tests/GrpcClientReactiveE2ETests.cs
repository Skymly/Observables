using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Grpc.Net.Client;
using Observables.Grpc;
using Observables.Grpc.Reactive.Tests.Contracts;
using Observables.Grpc.Tests.Infrastructure;
using Observables.Grpc.Tests.Protos;

namespace Observables.Grpc.Reactive.Tests;

[Collection(nameof(GrpcTestHostCollection))]
public sealed class GrpcClientReactiveE2ETests(GrpcTestHostFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnaryEcho_returns_response()
    {
        using var channel = GrpcChannel.ForAddress(fixture.Host.Address);
        var client = GrpcService.For<IE2EReactiveHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var reply = await client
            .UnaryEcho(new EchoRequest { Text = "hello-reactive-grpc" }, cts.Token)
            .Timeout(DefaultTimeout)
            .FirstAsync()
            .ToTask();

        Assert.Equal("hello-reactive-grpc", reply.Text);
    }
}
