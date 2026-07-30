namespace Observables.Postgres.Reactive.Tests.Contracts;

/// <summary>Non-JSON wire format used to prove custom serializer registration.</summary>
public sealed class ColonDelimitedPayload
{
    public string Kind { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
