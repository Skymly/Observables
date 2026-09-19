using Grpc.Core;
using Observables.Grpc;
using R3;

namespace Observables.NuGetSmoke.Grpc.R3.NoTransport;

[Grpc("echo.Echo")]
public interface ISmokeHub
{
    [GrpcUnary("UnaryEcho")]
    Observable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);
}

public static class Program
{
    public static void Main()
    {
        ISmokeHub hub = GrpcService.For<ISmokeHub>(new SmokeCallInvoker());
        if (hub is null)
        {
            throw new InvalidOperationException("Grpc R3 no-transport generated proxy was not created.");
        }

        Console.WriteLine("Observables.Grpc.R3 no-transport consumer smoke OK");
    }
}

sealed class SmokeCallInvoker : CallInvoker
{
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => throw new NotSupportedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        => throw new NotSupportedException();
}
