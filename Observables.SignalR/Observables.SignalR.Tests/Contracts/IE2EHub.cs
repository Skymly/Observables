using Observables.SignalR;
using R3;

namespace Observables.SignalR.Tests.Contracts;

[Hub]
public interface IE2EHub
{
    [HubInvoke]
    Observable<int> Add(int a, int b);

    [HubSend]
    Observable<Unit> EchoSend(string text);

    [HubStream]
    Observable<int> Counter(int max);

    [HubOn("Notify")]
    Observable<string> Notify { get; }
}
