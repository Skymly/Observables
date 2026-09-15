using Observables.Mqtt;
using R3;

namespace Observables.Mqtt.Tests.Contracts;

/// <summary>Named counterpart of <see cref="IE2EHub"/>; resolved through <c>MqttService.For&lt;T&gt;()</c>.</summary>
[Mqtt("e2e-named")]
public interface IE2ENamedHub
{
    [MqttSubscribe("e2e/named")]
    Observable<string> Ping { get; }

    [MqttPublish("e2e/named")]
    Observable<Unit> PublishPing();
}

/// <summary>Carries a name that no test registers a client for.</summary>
[Mqtt("e2e-unregistered")]
public interface IE2EUnregisteredHub
{
    [MqttSubscribe("e2e/unregistered")]
    Observable<string> Ping { get; }
}
