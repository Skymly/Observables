using Microsoft.CodeAnalysis;

namespace Observables.WebSocket.Generators;

internal static class DiagnosticDescriptors
{
#pragma warning disable RS2008
    public static readonly DiagnosticDescriptor InvalidWebSocketMember =
        new(
            "OBS6001",
            "WebSocket interface members must declare a WebSocket boundary attribute",
            "Member {0}.{1} has no WebSocketSend, WebSocketReceive, WebSocketConnect, or WebSocketClose attribute",
            "Observables.WebSocket",
            DiagnosticSeverity.Warning,
            true);

    public static readonly DiagnosticDescriptor WebSocketCoreNotReferenced =
        new(
            "OBS6002",
            "Observables.WebSocket must be referenced",
            "Observables.WebSocket is not referenced. Add a PackageReference to Observables.WebSocket.",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS6003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.WebSocket",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS6004",
            "Member shape mismatch for WebSocket boundary",
            "Member '{0}' does not match its WebSocket boundary attribute",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS6005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.WebSocket.Reactive",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedWebSocketOption =
        new(
            "OBS6006",
            "Unsupported WebSocket option or member shape",
            "Member '{0}.{1}' uses an unsupported shape or parameter combination",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            true);
#pragma warning restore RS2008
}

internal static class WebSocketGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildWebSocket = "BuildWebSocket";
}
