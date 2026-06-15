namespace Observables.Nats;

#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

/// <summary>Payload conversion helpers shared by R3 and Reactive NATS bridges.</summary>
#if NET8_0_OR_GREATER
[RequiresUnreferencedCode(NatsTrimAnnotations.JsonPayload)]
[RequiresDynamicCode(NatsTrimAnnotations.JsonPayload)]
#endif
internal static class NatsPayload
{
    internal static T Deserialize<T>(byte[] payload) => NatsPayloadSerializers.Deserialize<T>(payload);
}
