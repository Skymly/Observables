using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.Nats.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidNatsMember =
        new(
            "OBS9001",
            "Nats interface members must declare a Nats boundary attribute",
            "Member {0}.{1} has no NatsPublish, NatsRequest, or NatsSubscribe attribute, or uses a non-literal subject template",
            "Observables.Nats",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Nats member missing boundary attribute or non-literal subject template.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9001"));

    public static readonly DiagnosticDescriptor NatsCoreNotReferenced =
        new(
            "OBS9002",
            "Observables.Nats must be referenced",
            "Observables.Nats is not referenced. Add a PackageReference to Observables.Nats.",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.Nats runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS9003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Nats",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on a Nats member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS9004",
            "Member shape mismatch for Nats boundary",
            "Member '{0}' does not match its Nats boundary attribute (methods vs properties)",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the Nats boundary attribute (for example, [NatsSubscribe] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS9004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS9005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Nats.Reactive",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.Nats.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9005"));

    public static readonly DiagnosticDescriptor UnsupportedNatsOption =
        new(
            "OBS9006",
            "Unsupported Nats option or payload shape",
            "Member '{0}.{1}' uses an unsupported subject template, extra parameters, or subscribe placeholder syntax",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unsupported subject template, extra parameters, or subscribe placeholder syntax.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9006"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS9008",
            "Internal source generator error",
            "An internal error occurred in the Nats source generator: {0}: {1}",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the Nats source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS9008"));
}

internal static class NatsGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildNats = "BuildNats";
}
