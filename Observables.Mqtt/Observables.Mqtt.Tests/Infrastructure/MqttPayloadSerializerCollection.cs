namespace Observables.Mqtt.Tests.Infrastructure;

/// <summary>
/// Tests that mutate the process-wide <c>MqttPayloadSerializers</c> registry.
/// <c>DisableParallelization</c> keeps them from overlapping any test that deserializes through it.
/// </summary>
[CollectionDefinition(nameof(MqttPayloadSerializerCollection), DisableParallelization = true)]
public sealed class MqttPayloadSerializerCollection;
