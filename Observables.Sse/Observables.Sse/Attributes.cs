namespace Observables.Sse;

/// <summary>Marks a Server-Sent Events (SSE) proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class SseAttribute(string? endpointName = null) : Attribute
{
    public string? EndpointName { get; } = endpointName;
}

/// <summary>
/// Maps a property to a named SSE event stream. When <paramref name="eventName"/> is null,
/// the property subscribes to the default SSE event type ("message").
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SseEventAttribute(string? eventName = null) : Attribute
{
    public string? EventName { get; } = eventName;
}
