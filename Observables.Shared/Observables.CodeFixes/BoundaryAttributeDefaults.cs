namespace Observables.CodeFixes;

internal static class BoundaryAttributeDefaults
{
    internal static string MethodAttribute(ObservablesMemberDiagnosticIds.InterfaceProxyDomain domain, string memberName) =>
        domain switch
        {
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.SignalR => $"[HubInvoke(\"{memberName}\")]",
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Mqtt => $"[MqttPublish(\"{memberName}\")]",
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.WebSocket => $"[WebSocketSend(\"{memberName}\")]",
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        };

    internal static string PropertyAttribute(ObservablesMemberDiagnosticIds.InterfaceProxyDomain domain, string memberName) =>
        domain switch
        {
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.SignalR => $"[HubOn(\"{memberName}\")]",
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Mqtt => $"[MqttSubscribe(\"{memberName}\")]",
            ObservablesMemberDiagnosticIds.InterfaceProxyDomain.WebSocket => $"[WebSocketReceive(\"{memberName}\")]",
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        };

    internal static bool RequiresProperty(string attributeName) =>
        attributeName is "HubOnAttribute" or "MqttSubscribeAttribute" or "WebSocketReceiveAttribute";

    internal static bool RequiresMethod(string attributeName) =>
        attributeName is "HubInvokeAttribute" or "HubSendAttribute" or "HubStreamAttribute"
            or "MqttPublishAttribute"
            or "WebSocketSendAttribute" or "WebSocketConnectAttribute" or "WebSocketCloseAttribute";
}
