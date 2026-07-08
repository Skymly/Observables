using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

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
            isEnabledByDefault: true,
            description: "Mqtt member missing boundary attribute or non-literal topic template.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5001"));

    public static readonly DiagnosticDescriptor MqttCoreNotReferenced =
        new(
            "OBS5002",
            "Observables.Mqtt must be referenced",
            "Observables.Mqtt is not referenced. Add a PackageReference to Observables.Mqtt.",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.Mqtt runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS5003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Mqtt",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on an Mqtt member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS5004",
            "Member shape mismatch for Mqtt boundary",
            "Member '{0}' does not match its Mqtt boundary attribute (methods vs properties)",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the Mqtt boundary attribute (for example, [MqttSubscribe] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS5004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS5005",
            "Observables.Mqtt.Reactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Mqtt.Reactive",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.Mqtt.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5005"));

    public static readonly DiagnosticDescriptor UnsupportedMqttOption =
        new(
            "OBS5006",
            "Unsupported Mqtt option or payload shape",
            "Member '{0}.{1}' uses an unsupported topic template, extra parameters, or subscribe placeholder syntax",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unsupported topic template, extra parameters, or subscribe placeholder syntax.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5006"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS5008",
            "Internal source generator error",
            "An internal error occurred in the Mqtt source generator: {0}: {1}",
            "Observables.Mqtt",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the Mqtt source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS5008"));
}

internal static class MqttGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildMqtt = "BuildMqtt";
}
