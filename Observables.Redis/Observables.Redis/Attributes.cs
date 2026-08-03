namespace Observables.Redis;

/// <summary>Marks a Redis Pub/Sub proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RedisAttribute(string? connectionName = null) : Attribute
{
    public string? ConnectionName { get; } = connectionName;
}

/// <summary>Client publish mapped to Redis <c>PUBLISH</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RedisPublishAttribute(string? channelTemplate = null) : Attribute
{
    public string? ChannelTemplate { get; } = channelTemplate;
}

/// <summary>Exact Channel subscription mapped to Redis <c>SUBSCRIBE</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RedisSubscribeAttribute(string? channel = null) : Attribute
{
    public string? Channel { get; } = channel;
}
