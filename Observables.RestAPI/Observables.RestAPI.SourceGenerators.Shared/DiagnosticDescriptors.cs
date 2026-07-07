using Microsoft.CodeAnalysis;

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
            true
        );

    public static readonly DiagnosticDescriptor RestApiCoreNotReferenced =
        new(
            "OBS3002",
            "Observables.RestAPI must be referenced",
            "Observables.RestAPI is not referenced. Add a PackageReference to Observables.RestAPI.",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            true
        );

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS3003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.RestAPI",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            true
        );

    public static readonly DiagnosticDescriptor PathParameterMismatch =
        new(
            "OBS3004",
            "Path template mismatch",
            "Path template for method '{0}' does not match its parameters",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            true
        );

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS3005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.RestAPI.Reactive",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            true
        );

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS3006",
            "Internal source generator error",
            "An internal error occurred in the RestAPI source generator: {0}: {1}",
            "Observables.RestAPI",
            DiagnosticSeverity.Error,
            true
        );
}

internal static class RestApiGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildRestApi = "BuildRestApi";
}
