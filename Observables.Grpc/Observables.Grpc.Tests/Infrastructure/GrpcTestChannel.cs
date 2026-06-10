using Grpc.Net.Client;

namespace Observables.Grpc.Tests.Infrastructure;

/// <summary>Creates gRPC channels wired to the in-memory <see cref="GrpcTestHost"/>.</summary>
internal static class GrpcTestChannel
{
    public static GrpcChannel Create(GrpcTestHost host) =>
        GrpcChannel.ForAddress(host.Address, new GrpcChannelOptions
        {
            HttpHandler = host.CreateHandler(),
        });
}
