using System.Collections.Immutable;

namespace Observables.CodeFixes;

internal static class ObservablesPackageReferenceMappings
{
    internal static readonly ImmutableDictionary<string, string> RuntimePackageByDiagnosticId =
        new Dictionary<string, string>
        {
            ["OBS3002"] = "Observables.RestAPI",
            ["OBS4002"] = "Observables.SignalR",
            ["OBS5002"] = "Observables.Mqtt",
            ["OBS6002"] = "Observables.WebSocket",
            ["OBS7002"] = "Observables.Grpc",
            ["OBS8002"] = "Observables.Sse",
            ["OBS9002"] = "Observables.Nats",
            ["OBS10002"] = "Observables.Postgres",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static readonly ImmutableDictionary<string, string> ReactivePackageByDiagnosticId =
        new Dictionary<string, string>
        {
            ["OBS3005"] = "Observables.RestAPI.Reactive",
            ["OBS4005"] = "Observables.SignalR.Reactive",
            ["OBS5005"] = "Observables.Mqtt.Reactive",
            ["OBS6005"] = "Observables.WebSocket.Reactive",
            ["OBS7005"] = "Observables.Grpc.Reactive",
            ["OBS8005"] = "Observables.Sse.Reactive",
            ["OBS9005"] = "Observables.Nats.Reactive",
            ["OBS10005"] = "Observables.Postgres.Reactive",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static readonly ImmutableDictionary<string, string> R3PackageByReactivePackageId =
        new Dictionary<string, string>
        {
            ["Observables.RestAPI.Reactive"] = "Observables.RestAPI.R3",
            ["Observables.SignalR.Reactive"] = "Observables.SignalR.R3",
            ["Observables.Mqtt.Reactive"] = "Observables.Mqtt.R3",
            ["Observables.WebSocket.Reactive"] = "Observables.WebSocket.R3",
            ["Observables.Grpc.Reactive"] = "Observables.Grpc.R3",
            ["Observables.Sse.Reactive"] = "Observables.Sse.R3",
            ["Observables.Nats.Reactive"] = "Observables.Nats.R3",
            ["Observables.Postgres.Reactive"] = "Observables.Postgres.R3",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    internal static bool TryGetRuntimePackage(string diagnosticId, out string packageId) =>
        RuntimePackageByDiagnosticId.TryGetValue(diagnosticId, out packageId!);

    internal static bool TryGetReactivePackage(string diagnosticId, out string packageId) =>
        ReactivePackageByDiagnosticId.TryGetValue(diagnosticId, out packageId!);
}
