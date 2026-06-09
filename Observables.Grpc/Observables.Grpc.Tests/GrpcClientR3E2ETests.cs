using Grpc.Net.Client;
using Observables.Grpc;
using Observables.Grpc.Tests.Contracts;
using Observables.Grpc.Tests.Infrastructure;
using Observables.Grpc.Tests.Protos;
using R3;

namespace Observables.Grpc.Tests;

[Collection(nameof(GrpcTestHostCollection))]
public sealed class GrpcClientR3E2ETests(GrpcTestHostFixture fixture)
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UnaryEcho_returns_response()
    {
        using var channel = GrpcChannel.ForAddress(fixture.Host.Address);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var reply = await client
            .UnaryEcho(new EchoRequest { Text = "hello-grpc" }, cts.Token)
            .FirstAsync(cts.Token);

        Assert.Equal("hello-grpc", reply.Text);
    }

    [Fact]
    public async Task ServerStreamEcho_emits_multiple_items()
    {
        using var channel = GrpcChannel.ForAddress(fixture.Host.Address);
        var client = GrpcService.For<IE2EHub>(channel.CreateCallInvoker());

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var replies = await client
            .ServerStreamEcho(new EchoRequest { Text = "stream" }, cts.Token)
            .Take(3)
            .ToArrayAsync(cts.Token);

        Assert.Equal(3, replies.Length);
        Assert.Equal("stream-0", replies[0].Text);
        Assert.Equal("stream-2", replies[2].Text);
    }
}
