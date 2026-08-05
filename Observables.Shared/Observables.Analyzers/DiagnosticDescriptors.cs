using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

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
            description: "Both .R3 and .Reactive Observables packages are referenced for the same feature.",
            helpLinkUri: DiagnosticHelpLink.For("OBS0001"),
            customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor EmptyHubInterface =
        new(
            "OBS4007",
            "Empty hub proxy interface",
            "Interface '{0}' is marked with [Hub] but declares no members. Add hub boundary members or remove [Hub].",
            "Observables.SignalR",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Hub] interface (SignalR).",
            helpLinkUri: DiagnosticHelpLink.For("OBS4007"));

    public static readonly DiagnosticDescriptor EmptyMqttInterface =
        new(
            "OBS5007",
            "Empty MQTT proxy interface",
            "Interface '{0}' is marked with [Mqtt] but declares no members. Add MQTT boundary members or remove [Mqtt].",
            "Observables.Mqtt",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Mqtt] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5007"));

    public static readonly DiagnosticDescriptor EmptyWebSocketInterface =
        new(
            "OBS6007",
            "Empty WebSocket proxy interface",
            "Interface '{0}' is marked with [WebSocket] but declares no members. Add WebSocket boundary members or remove [WebSocket].",
            "Observables.WebSocket",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [WebSocket] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS6007"));

    public static readonly DiagnosticDescriptor EmptyGrpcInterface =
        new(
            "OBS7007",
            "Empty gRPC proxy interface",
            "Interface '{0}' is marked with [Grpc] but declares no members. Add gRPC boundary members or remove [Grpc].",
            "Observables.Grpc",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Grpc] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS7007"));

    public static readonly DiagnosticDescriptor EmptySseInterface =
        new(
            "OBS8007",
            "Empty SSE proxy interface",
            "Interface '{0}' is marked with [Sse] but declares no members. Add SSE boundary members or remove [Sse].",
            "Observables.Sse",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Sse] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS8007"));

    public static readonly DiagnosticDescriptor EmptyNatsInterface =
        new(
            "OBS9007",
            "Empty NATS proxy interface",
            "Interface '{0}' is marked with [Nats] but declares no members. Add NATS boundary members or remove [Nats].",
            "Observables.Nats",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Nats] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9007"));

    public static readonly DiagnosticDescriptor EmptyPostgresInterface =
        new(
            "OBS10007",
            "Empty Postgres proxy interface",
            "Interface '{0}' is marked with [Postgres] but declares no members. Add LISTEN/NOTIFY boundary members or remove [Postgres].",
            "Observables.Postgres",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Postgres] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10007"));

    public static readonly DiagnosticDescriptor EmptyRedisInterface =
        new(
            "OBS11007",
            "Empty Redis proxy interface",
            "Interface '{0}' is marked with [Redis] but declares no members. Add Redis Pub/Sub boundary members or remove [Redis].",
            "Observables.Redis",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [Redis] interface.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11007"));

    public static readonly DiagnosticDescriptor EmptyRestApiInterface =
        new(
            "OBS3007",
            "Empty RestAPI proxy interface",
            "Interface '{0}' is marked with HTTP method attributes but declares no valid members. Add HTTP boundary members or remove the attributes.",
            "Observables.RestAPI",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Empty [RestApi] interface (RestAPI).",
            helpLinkUri: DiagnosticHelpLink.For("OBS3007"));
}
