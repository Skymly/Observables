namespace Observables.Mqtt;

/// <summary>Reports optional features available on the connected MQTT transport.</summary>
[Flags]
public enum MqttTransportCapabilities
{
    /// <summary>The transport supports the basic publish and subscribe contract.</summary>
    None = 0,

    /// <summary>The connection supports MQTT 5 response topics and correlation data.</summary>
    RequestResponse = 1,
}

/// <summary>Provides the broker operations used by generated MQTT proxies.</summary>
/// <remarks>
/// The caller owns the connection. Implementations report rejected broker operations as exceptions
/// and use a stable transport instance for a given client so proxies can share broker subscriptions.
/// </remarks>
public interface IMqttTransport
{
    /// <summary>Gets the optional features available on the current connection.</summary>
    MqttTransportCapabilities Capabilities { get; }

    /// <summary>
    /// Notifies subscribers of a received topic and payload. The payload remains valid until the
    /// handler's task completes. A handler that needs to retain the payload must copy it.
    /// </summary>
    event Func<string, ReadOnlyMemory<byte>, Task>? MessageReceived;

    /// <summary>Subscribes to a broker topic filter at QoS 0.</summary>
    Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken);

    /// <summary>Removes the broker subscription for a topic filter.</summary>
    Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken);

    /// <summary>Publishes a payload at QoS 1.</summary>
    Task PublishAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
