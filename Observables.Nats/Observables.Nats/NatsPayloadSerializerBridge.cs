using System.Buffers;
using NATS.Client.Core;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Nats;

/// <summary>
/// Adapts <see cref="NatsPayloadSerializers"/> to the NATS client serializer interfaces so that
/// generated proxies route payloads through the registered serializer.
/// </summary>
/// <remarks>
/// <see cref="ForOrNull"/> returns <see langword="null"/> when <see cref="NatsPayloadSerializers"/>
/// cannot round-trip <typeparamref name="T"/>, which hands the payload back to the NATS client
/// serializer registry.
/// </remarks>
internal sealed class NatsPayloadSerializerBridge<T> : INatsSerialize<T>, INatsDeserialize<T>
{
    static readonly NatsPayloadSerializerBridge<T> Instance = new();

    NatsPayloadSerializerBridge()
    {
    }

#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "CanRoundTrip only inspects registrations and typeof comparisons; it never serializes.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "CanRoundTrip only inspects registrations and typeof comparisons; it never serializes.")]
#endif
    internal static NatsPayloadSerializerBridge<T>? ForOrNull() =>
        NatsPayloadSerializers.CanRoundTrip<T>() ? Instance : null;

#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Every NatsProtocol entry point that reaches this bridge is annotated RequiresUnreferencedCode.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Every NatsProtocol entry point that reaches this bridge is annotated RequiresDynamicCode.")]
#endif
    public void Serialize(IBufferWriter<byte> bufferWriter, T value) =>
        bufferWriter.Write(NatsPayloadSerializers.Serialize(value));

#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Every NatsProtocol entry point that reaches this bridge is annotated RequiresUnreferencedCode.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Every NatsProtocol entry point that reaches this bridge is annotated RequiresDynamicCode.")]
#endif
    public T Deserialize(in ReadOnlySequence<byte> buffer) =>
        buffer.IsSingleSegment
            ? NatsPayloadSerializers.Deserialize<T>(buffer.First.Span)
            : NatsPayloadSerializers.Deserialize<T>(buffer.ToArray());
}
