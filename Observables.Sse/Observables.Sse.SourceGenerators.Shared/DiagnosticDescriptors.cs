using Microsoft.CodeAnalysis;

namespace Observables.Sse.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidSseMember =
        new(
            "OBS8001",
            "SSE interface members must declare an SSE boundary attribute",
            "Member {0}.{1} has no SseEvent attribute",
            "Observables.Sse",
            DiagnosticSeverity.Warning,
            true);

    public static readonly DiagnosticDescriptor SseCoreNotReferenced =
        new(
            "OBS8002",
            "Observables.Sse must be referenced",
            "Observables.Sse is not referenced. Add a PackageReference to Observables.Sse.",
            "Observables.Sse",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS8003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Sse",
            "Observables.Sse",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS8004",
            "Member shape mismatch for SSE boundary",
            "Member '{0}' does not match its SSE boundary attribute; [SseEvent] must be applied to a property",
            "Observables.Sse",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS8005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Sse.Reactive",
            "Observables.Sse",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS8006",
            "Internal source generator error",
            "An internal error occurred in the Sse source generator: {0}: {1}",
            "Observables.Sse",
            DiagnosticSeverity.Error,
            true);
}

internal static class SseGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildSse = "BuildSse";
}
