namespace Observables.Postgres;

/// <summary>Marks a PostgreSQL LISTEN/NOTIFY proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class PostgresAttribute : Attribute;

/// <summary>LISTEN channel mapped to a hot notification stream.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ListenAttribute(string? channel = null) : Attribute
{
    public string? Channel { get; } = channel;
}

/// <summary>NOTIFY channel mapped to a cold send stream.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NotifyAttribute(string? channel = null) : Attribute
{
    public string? Channel { get; } = channel;
}
