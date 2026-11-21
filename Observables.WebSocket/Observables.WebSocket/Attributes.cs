namespace Observables.WebSocket;

/// <summary>Marks a WebSocket proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class WebSocketAttribute : Attribute;

/// <summary>Client-to-server message send mapped to WebSocket send.</summary>
/// <remarks>
/// Each subscription serializes any JSON payload before sending.
/// Wait for each send on the same socket to complete before starting the next.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WebSocketSendAttribute(string? messageName = null) : Attribute
{
    public string? MessageName { get; } = messageName;
}

/// <summary>Server-to-client message receive mapped to incoming WebSocket messages.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class WebSocketReceiveAttribute(string? messageName = null) : Attribute
{
    public string? MessageName { get; } = messageName;
}

/// <summary>WebSocket connection initiation.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WebSocketConnectAttribute : Attribute;

/// <summary>WebSocket connection close.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WebSocketCloseAttribute : Attribute;
