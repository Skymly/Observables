using Microsoft.CodeAnalysis;

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
            true);

    public static readonly DiagnosticDescriptor NatsCoreNotReferenced =
        new(
            "OBS9002",
            "Observables.Nats must be referenced",
            "Observables.Nats is not referenced. Add a PackageReference to Observables.Nats.",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS9003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Nats",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS9004",
            "Member shape mismatch for Nats boundary",
            "Member '{0}' does not match its Nats boundary attribute (methods vs properties)",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS9005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Nats.Reactive",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedNatsOption =
        new(
            "OBS9006",
            "Unsupported Nats option or payload shape",
            "Member '{0}.{1}' uses an unsupported subject template, extra parameters, or subscribe placeholder syntax",
            "Observables.Nats",
            DiagnosticSeverity.Error,
            true);
}

internal static class NatsGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildNats = "BuildNats";
}
