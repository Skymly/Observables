using Microsoft.CodeAnalysis;

namespace Observables.SignalR.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidHubMember =
        new(
            "OBS4001",
            "Hub interface members must declare a SignalR boundary attribute",
            "Member {0}.{1} has no HubInvoke, HubSend, HubStream, or HubOn attribute, or uses a non-literal method name",
            "Observables.SignalR",
            DiagnosticSeverity.Warning,
            true);

    public static readonly DiagnosticDescriptor SignalRCoreNotReferenced =
        new(
            "OBS4002",
            "Observables.SignalR must be referenced",
            "Observables.SignalR is not referenced. Add a PackageReference to Observables.SignalR.",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS4003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.SignalR",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS4004",
            "Member shape mismatch for SignalR boundary",
            "Member '{0}' does not match its Hub boundary attribute (methods vs properties)",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS4005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.SignalR.Reactive",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedStreamingParameter =
        new(
            "OBS4006",
            "Unsupported streaming parameter",
            "Parameter '{0}' on method '{1}' uses client-to-server streaming, which is not supported in this release",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS4008",
            "Internal source generator error",
            "An internal error occurred in the SignalR source generator: {0}: {1}",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            true);
}

internal static class SignalRGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildSignalR = "BuildSignalR";
}
