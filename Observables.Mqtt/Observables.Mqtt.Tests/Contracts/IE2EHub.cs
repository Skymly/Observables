using Observables.Mqtt;
using R3;

namespace Observables.Mqtt.Tests.Contracts;

[Mqtt]
public interface IE2EHub
{
    [MqttSubscribe("e2e/ping")]
    Observable<string> Ping { get; }

    [MqttPublish("e2e/ping")]
    Observable<Unit> PublishPing();
}
