using Microsoft.CodeAnalysis;

namespace Observables.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor ConflictingReactivePackages =
        new(
            "OBS0001",
            "Conflicting Observables reactive packages",
            "Both R3 and System.Reactive Observables packages are referenced for {0}. Remove either the .R3 or .Reactive package for this feature.",
            "Observables",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor EmptyHubInterface =
        new(
            "OBS4007",
            "Empty hub proxy interface",
            "Interface '{0}' is marked with [Hub] but declares no members. Add hub boundary members or remove [Hub].",
            "Observables.SignalR",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyMqttInterface =
        new(
            "OBS5007",
            "Empty MQTT proxy interface",
            "Interface '{0}' is marked with [Mqtt] but declares no members. Add MQTT boundary members or remove [Mqtt].",
            "Observables.Mqtt",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyWebSocketInterface =
        new(
            "OBS6007",
            "Empty WebSocket proxy interface",
            "Interface '{0}' is marked with [WebSocket] but declares no members. Add WebSocket boundary members or remove [WebSocket].",
            "Observables.WebSocket",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyGrpcInterface =
        new(
            "OBS7007",
            "Empty gRPC proxy interface",
            "Interface '{0}' is marked with [Grpc] but declares no members. Add gRPC boundary members or remove [Grpc].",
            "Observables.Grpc",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptySseInterface =
        new(
            "OBS8007",
            "Empty SSE proxy interface",
            "Interface '{0}' is marked with [Sse] but declares no members. Add SSE boundary members or remove [Sse].",
            "Observables.Sse",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyNatsInterface =
        new(
            "OBS9007",
            "Empty NATS proxy interface",
            "Interface '{0}' is marked with [Nats] but declares no members. Add NATS boundary members or remove [Nats].",
            "Observables.Nats",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyRestApiInterface =
        new(
            "OBS3007",
            "Empty RestAPI proxy interface",
            "Interface '{0}' is marked with HTTP method attributes but declares no valid members. Add HTTP boundary members or remove the attributes.",
            "Observables.RestAPI",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
}
