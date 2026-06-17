namespace Observables.SignalR;

/// <summary>Marks a hub proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class HubAttribute : Attribute;

/// <summary>Client invoke (single result) mapped to HubConnection.InvokeAsync.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HubInvokeAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Client send (no result) mapped to HubConnection.SendAsync.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HubSendAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Client stream mapped to HubConnection.StreamAsync.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HubStreamAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Server callback mapped to HubConnection.On.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HubOnAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}
