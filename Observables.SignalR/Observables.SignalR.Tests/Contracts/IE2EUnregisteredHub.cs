using Observables.SignalR;
using R3;

namespace Observables.SignalR.Tests.Contracts;

/// <summary>Carries a name that no test registers a connection for.</summary>
[Hub("e2e-unregistered")]
public interface IE2EUnregisteredHub
{
    [HubInvoke]
    Observable<int> Add(int a, int b);
}
