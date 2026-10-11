namespace Observables.Postgres.Tests.Infrastructure;

/// <summary>
/// Tests that mutate the process-wide <c>PostgresPayloadSerializers</c> registry.
/// <c>DisableParallelization</c> keeps them from overlapping any test that deserializes through it.
/// </summary>
[CollectionDefinition(nameof(PostgresPayloadSerializerCollection), DisableParallelization = true)]
public sealed class PostgresPayloadSerializerCollection;
