using Microsoft.CodeAnalysis;

namespace Observables.Grpc.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidGrpcMember =
        new(
            "OBS7001",
            "Grpc interface members must declare a Grpc boundary attribute",
            "Member {0}.{1} has no GrpcUnary, GrpcServerStream, GrpcClientStream, or GrpcDuplex attribute",
            "Observables.Grpc",
            DiagnosticSeverity.Warning,
            true);

    public static readonly DiagnosticDescriptor GrpcCoreNotReferenced =
        new(
            "OBS7002",
            "Observables.Grpc must be referenced",
            "Observables.Grpc is not referenced. Add a PackageReference to Observables.Grpc.",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS7003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Grpc",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS7004",
            "Member shape mismatch for Grpc boundary",
            "Member '{0}' does not match its Grpc boundary attribute",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS7005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Grpc.Reactive",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor UnsupportedGrpcOption =
        new(
            "OBS7006",
            "Unsupported Grpc option or member shape",
            "Member '{0}.{1}' uses an unsupported shape or parameter combination",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS7008",
            "Internal source generator error",
            "An internal error occurred in the Grpc source generator: {0}: {1}",
            "Observables.Grpc",
            DiagnosticSeverity.Error,
            true);
}

internal static class GrpcGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildGrpc = "BuildGrpc";
}
