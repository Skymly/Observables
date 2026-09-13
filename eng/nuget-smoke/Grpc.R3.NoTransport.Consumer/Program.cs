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
    public static void Main() => Console.WriteLine("Observables.Grpc.R3 no-transport consumer smoke OK");
}
