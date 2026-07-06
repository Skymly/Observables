using Microsoft.CodeAnalysis;

namespace Observables.Mqtt.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidMqttMember =
        new(
            "OBS5001",
            "Mqtt interface members must declare a Mqtt boundary attribute",
            "Member {0}.{1} has no MqttPublish or MqttSubscribe attribute, or uses a non-literal topic template",
            "Observables.Mqtt",
            DiagnosticSeverity.Warning,
            true);

    public static readonly DiagnosticDescriptor MqttCoreNotReferenced =
        new(
            "OBS5002",
            "Observables.Mqtt must be referenced",
            "Observables.Mqtt is not referenced. Add a PackageReference to Observables.Mqtt.",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS5003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Mqtt",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS5004",
            "Member shape mismatch for Mqtt boundary",
            "Member '{0}' does not match its Mqtt boundary attribute (methods vs properties)",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS5005",
            "Observables.Mqtt.Reactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Mqtt.Reactive",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedMqttOption =
        new(
            "OBS5006",
            "Unsupported Mqtt option or payload shape",
            "Member '{0}.{1}' uses an unsupported topic template, extra parameters, or subscribe placeholder syntax",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS5008",
            "Internal source generator error",
            "An internal error occurred in the Mqtt source generator: {0}: {1}",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            true);
}

internal static class MqttGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildMqtt = "BuildMqtt";
}
