namespace Observables.Mqtt;

/// <summary>Marks a topic proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class MqttAttribute(string? clientName = null) : Attribute
{
    public string? ClientName { get; } = clientName;
}

/// <summary>
/// Client publish mapped to MQTT publish.
/// </summary>
/// <remarks>
/// v1 generated publish members are empty-payload commands: method parameters bind topic template
/// placeholders only. Payload, QoS, and retain are not a generated surface. Hand-written
/// <c>FromPublish</c> overloads that accept a payload remain available on the R3 bridge.
/// Generated publish uses QoS 1 and retain=false.
/// </remarks>
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
