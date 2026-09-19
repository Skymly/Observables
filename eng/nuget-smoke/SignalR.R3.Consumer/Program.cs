using Microsoft.AspNetCore.SignalR.Client;
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
    public static void Main()
    {
        HubConnection connection = new HubConnectionBuilder().WithUrl("http://127.0.0.1").Build();
        ISmokeHub hub = HubService.For<ISmokeHub>(connection);
        if (hub is null)
        {
            throw new InvalidOperationException("SignalR R3 generated proxy was not created.");
        }

        Console.WriteLine("Observables.SignalR.R3 consumer smoke OK");
    }
}
