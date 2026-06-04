using Observables.SignalR;
using System.Reactive;

namespace Observables.SignalR.Reactive.Tests.Contracts;

[Hub]
public interface IE2EHub
{
    [HubInvoke]
    IObservable<int> Add(int a, int b);

    [HubSend]
    IObservable<Unit> EchoSend(string text);

    [HubStream]
    IObservable<int> Counter(int max);

    [HubOn("Notify")]
    IObservable<string> Notify { get; }
}
