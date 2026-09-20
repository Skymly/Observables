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

/// <summary>
/// Channel subscription mapped to Redis <c>SUBSCRIBE</c> or <c>PSUBSCRIBE</c>.
/// </summary>
/// <remarks>
/// When <see cref="Pattern"/> is <see langword="true"/>, the member maps to <c>PSUBSCRIBE</c>
/// even if <see cref="Channel"/> has no glob metacharacters (for example character classes
/// such as <c>[ab]</c>). When <see cref="Pattern"/> is <see langword="false"/>, the generator
/// uses <c>PSUBSCRIBE</c> only when <see cref="Channel"/> contains <c>*</c> or <c>?</c>.
/// Character classes are not treated as Pattern by that heuristic.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RedisSubscribeAttribute(string? channel = null) : Attribute
{
    public string? Channel { get; } = channel;

    /// <summary>
    /// When <see langword="true"/>, maps to Redis <c>PSUBSCRIBE</c>. When <see langword="false"/>,
    /// <c>*</c> and <c>?</c> still select Pattern; other glob syntax such as <c>[ab]</c> stays literal.
    /// </summary>
    public bool Pattern { get; set; }
}
