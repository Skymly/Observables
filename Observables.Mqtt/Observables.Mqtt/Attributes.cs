namespace Observables.Mqtt;

/// <summary>Marks a topic proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class MqttAttribute(string? clientName = null) : Attribute
{
    public string? ClientName { get; } = clientName;
}

/// <summary>Client publish mapped to MQTT publish.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MqttPublishAttribute(string? topicTemplate = null) : Attribute
{
    public string? TopicTemplate { get; } = topicTemplate;
}

/// <summary>Broker subscription mapped to incoming application messages.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MqttSubscribeAttribute(string? topicFilter = null) : Attribute
{
    public string? TopicFilter { get; } = topicFilter;
}
