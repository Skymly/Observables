using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.WebSocket.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidWebSocketMember =
        new(
            "OBS6001",
            "WebSocket interface members must declare a WebSocket boundary attribute",
            "Member {0}.{1} has no WebSocketSend, WebSocketReceive, WebSocketConnect, or WebSocketClose attribute",
            "Observables.WebSocket",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "WebSocket member missing boundary attribute.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6001"));

    public static readonly DiagnosticDescriptor WebSocketCoreNotReferenced =
        new(
            "OBS6002",
            "Observables.WebSocket must be referenced",
            "Observables.WebSocket is not referenced. Add a PackageReference to Observables.WebSocket.",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.WebSocket runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS6003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.WebSocket",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on a WebSocket member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS6004",
            "Member shape mismatch for WebSocket boundary",
            "Member '{0}' does not match its WebSocket boundary attribute",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the WebSocket boundary attribute (for example, [WebSocketReceive] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS6004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS6005",
            "Observables.WebSocket.Reactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.WebSocket.Reactive",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.WebSocket.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6005"));

    public static readonly DiagnosticDescriptor UnsupportedWebSocketOption =
        new(
            "OBS6006",
            "Unsupported WebSocket option or member shape",
            "Member '{0}.{1}' uses an unsupported shape or parameter combination",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unsupported shape or parameter combination on a WebSocket member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6006"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS6008",
            "Internal source generator error",
            "An internal error occurred in the WebSocket source generator: {0}: {1}",
            "Observables.WebSocket",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the WebSocket source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6008"));
}

internal static class WebSocketGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildWebSocket = "BuildWebSocket";
}
