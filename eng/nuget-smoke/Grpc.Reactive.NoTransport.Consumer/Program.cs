using Observables.Grpc;

namespace Observables.NuGetSmoke.Grpc.Reactive.NoTransport;

[Grpc("echo.Echo")]
public interface ISmokeHub
{
    [GrpcUnary("UnaryEcho")]
    IObservable<string> UnaryEcho(string request, CancellationToken cancellationToken = default);
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.Grpc.Reactive no-transport consumer smoke OK");
}
