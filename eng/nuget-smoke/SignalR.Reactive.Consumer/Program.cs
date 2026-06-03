using System.Reactive;
using Observables.SignalR;

namespace Observables.NuGetSmoke.SignalR.Reactive;

[Hub]
public interface ISmokeHub
{
    [HubInvoke]
    IObservable<int> Ping();
}

public static class Program
{
    public static void Main() => Console.WriteLine("Observables.SignalR.Reactive consumer smoke OK");
}
