using Observables.SignalR;
using R3;

namespace Observables.NuGetSmoke.SignalR.R3;

[Hub]
public interface ISmokeHub
{
    [HubInvoke]
    Observable<int> Ping();
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.SignalR.R3 consumer smoke OK");
}
