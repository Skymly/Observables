using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

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
            isEnabledByDefault: true,
            description: "Hub member missing boundary attribute or non-literal hub method name.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4001"));

    public static readonly DiagnosticDescriptor SignalRCoreNotReferenced =
        new(
            "OBS4002",
#if OBSERVABLES_R3
            "Observables.SignalR.R3 must be referenced",
            "Observables.SignalR.R3 is not referenced. Add a PackageReference to Observables.SignalR.R3.",
#else
            "Observables.SignalR.Reactive must be referenced",
            "Observables.SignalR.Reactive is not referenced. Add a PackageReference to Observables.SignalR.Reactive.",
#endif
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
#if OBSERVABLES_R3
            description: "Observables.SignalR.R3 package is not referenced.",
#else
            description: "Observables.SignalR.Reactive package is not referenced.",
#endif
            helpLinkUri: DiagnosticHelpLink.For("OBS4002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS4003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.SignalR",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type must be Observable<T> or IObservable<T>; HubSend requires Unit.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS4004",
            "Member shape mismatch for SignalR boundary",
            "Member '{0}' does not match its Hub boundary attribute (methods vs properties)",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the Hub boundary attribute (for example, [HubOn] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS4004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS4005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.SignalR.Reactive",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.SignalR.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4005"));

    public static readonly DiagnosticDescriptor UnsupportedStreamingParameter =
        new(
            "OBS4006",
            "Unsupported streaming parameter",
            "Parameter '{0}' on method '{1}' uses client-to-server streaming, which is not supported in this release",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Client-to-server streaming parameters are not supported in this release.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4006"));

    public static readonly DiagnosticDescriptor MultipleHubBoundaries =
        new(
            "OBS4009",
            "Multiple SignalR boundary attributes on one member",
            "Member '{0}' declares more than one of HubInvoke, HubSend, HubStream, or HubOn",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "A hub member must declare exactly one boundary attribute.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4009"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS4008",
            "Internal source generator error",
            "An internal error occurred in the SignalR source generator: {0}: {1}",
            "Observables.SignalR",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the SignalR source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS4008"));
}

internal static class SignalRGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildSignalR = "BuildSignalR";
}
