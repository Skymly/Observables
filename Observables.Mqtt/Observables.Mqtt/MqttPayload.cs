namespace Observables.Mqtt;

/// <summary>Payload conversion helpers shared by R3 and Reactive MQTT bridges.</summary>
internal static class MqttPayload
{
    internal static T Deserialize<T>(byte[] payload) => MqttPayloadSerializers.Deserialize<T>(payload);
}
