namespace Observables.Nats.Tests.Infrastructure;

/// <summary>
/// Tests that mutate the process-wide <c>NatsPayloadSerializers</c> registry.
/// <c>DisableParallelization</c> keeps them from overlapping any test that deserializes through it.
/// </summary>
[CollectionDefinition(nameof(NatsPayloadSerializerCollection), DisableParallelization = true)]
public sealed class NatsPayloadSerializerCollection;
