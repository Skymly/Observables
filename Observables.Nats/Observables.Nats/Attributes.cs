namespace Observables.Nats;

/// <summary>Marks a NATS subject proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class NatsAttribute(string? connectionName = null) : Attribute
{
    public string? ConnectionName { get; } = connectionName;
}

/// <summary>Client publish mapped to NATS publish.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NatsPublishAttribute(string? subjectTemplate = null) : Attribute
{
    public string? SubjectTemplate { get; } = subjectTemplate;
}

/// <summary>Request-reply mapped to NATS request.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NatsRequestAttribute(string? subjectTemplate = null) : Attribute
{
    public string? SubjectTemplate { get; } = subjectTemplate;
}

/// <summary>Subject subscription mapped to incoming messages.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NatsSubscribeAttribute(string? subjectFilter = null) : Attribute
{
    public string? SubjectFilter { get; } = subjectFilter;
}
