using Observables.Mqtt;
using System.Reactive;

namespace Observables.Mqtt.Reactive.Tests.Contracts;

[Mqtt]
public interface IE2EHub
{
    [MqttSubscribe("e2e/ping")]
    IObservable<string> Ping { get; }

    [MqttPublish("e2e/ping")]
    IObservable<Unit> PublishPing();
}
