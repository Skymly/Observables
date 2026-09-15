using Observables.SignalR;
using R3;

namespace Observables.SignalR.Tests.Contracts;

/// <summary>Named counterpart of <see cref="IE2EHub"/>; resolved through <c>HubService.For&lt;T&gt;()</c>.</summary>
[Hub("e2e-named")]
public interface IE2ENamedHub
{
    [HubInvoke]
    Observable<int> Add(int a, int b);
}
