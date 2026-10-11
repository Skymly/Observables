namespace Observables.Redis.Tests.Infrastructure;

/// <summary>
/// Tests that mutate the process-wide <c>RedisPayloadSerializers</c> registry.
/// <c>DisableParallelization</c> keeps them from overlapping any test that deserializes through it.
/// </summary>
[CollectionDefinition(nameof(RedisPayloadSerializerCollection), DisableParallelization = true)]
public sealed class RedisPayloadSerializerCollection;
