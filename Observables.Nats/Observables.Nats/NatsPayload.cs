namespace Observables.Nats;

/// <summary>Payload conversion helpers shared by R3 and Reactive MQTT bridges.</summary>
internal static class NatsPayload
{
    internal static T Deserialize<T>(byte[] payload) => NatsPayloadSerializers.Deserialize<T>(payload);
}
