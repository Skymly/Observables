using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.RestAPI.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidRestApiMember =
        new(
            "OBS3001",
            "Observables.RestAPI types must have HTTP method attributes",
            "Method {0}.{1} has no Observables.RestAPI HTTP method attribute or uses a non-literal path argument",
            "Observables.RestAPI",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Interface method missing HTTP verb attribute or non-literal path.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3001"));

    public static readonly DiagnosticDescriptor RestApiCoreNotReferenced =
        new(
            "OBS3002",
            "Observables.RestAPI must be referenced",
            "Observables.RestAPI is not referenced. Add a PackageReference to Observables.RestAPI.",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.RestAPI runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS3003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.RestAPI",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on an API method.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3003"));

    public static readonly DiagnosticDescriptor PathParameterMismatch =
        new(
            "OBS3004",
            "Path template mismatch",
            "Path template for method '{0}' does not match its parameters",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Path template does not match method parameters.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS3005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.RestAPI.Reactive",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.RestAPI.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3005"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS3006",
            "Internal source generator error",
            "An internal error occurred in the RestAPI source generator: {0}: {1}",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the RestAPI source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS3006"));
}

internal static class RestApiGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildRestApi = "BuildRestApi";
}
