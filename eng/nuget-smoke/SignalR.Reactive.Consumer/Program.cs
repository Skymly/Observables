using Microsoft.AspNetCore.SignalR.Client;
using Observables.SignalR;
using System.Reactive;

namespace Observables.NuGetSmoke.SignalR.Reactive;

[Hub]
public interface ISmokeHub
{
    [HubInvoke]
    IObservable<int> Ping();
}

public static class Program
{
    public static void Main()
    {
        HubConnection connection = new HubConnectionBuilder().WithUrl("http://127.0.0.1").Build();
        ISmokeHub hub = HubService.For<ISmokeHub>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("SignalR Reactive generated proxy was not created.");
        }

        Console.WriteLine("Observables.SignalR.Reactive consumer smoke OK");
    }
}
