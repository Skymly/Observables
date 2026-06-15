namespace Observables.Mqtt;

#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

/// <summary>Payload conversion helpers shared by R3 and Reactive MQTT bridges.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(MqttTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(MqttTrimAnnotations.JsonPayload)]
#endif
internal static class MqttPayload
{
    internal static T Deserialize<T>(byte[] payload) => MqttPayloadSerializers.Deserialize<T>(payload);
}
