using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.Postgres.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidPostgresMember =
        new(
            "OBS10001",
            "Postgres interface members must declare a Postgres boundary attribute",
            "Member {0}.{1} has no Listen or Notify attribute, or uses a non-literal channel name",
            "Observables.Postgres",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Postgres member missing boundary attribute or non-literal channel name.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10001"));

    public static readonly DiagnosticDescriptor PostgresCoreNotReferenced =
        new(
            "OBS10002",
            "Observables.Postgres must be referenced",
            "Observables.Postgres is not referenced. Add a PackageReference to Observables.Postgres.",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.Postgres runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS10003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Postgres",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on a Postgres member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS10004",
            "Member shape mismatch for Postgres boundary",
            "Member '{0}' does not match its Postgres boundary attribute (methods vs properties)",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the Postgres boundary attribute (for example, [Listen] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS10004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS10005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Postgres.Reactive",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.Postgres.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10005"));

    public static readonly DiagnosticDescriptor UnsupportedPostgresOption =
        new(
            "OBS10006",
            "Unsupported Postgres option or payload shape",
            "Member '{0}.{1}' uses an unsupported channel name, placeholder syntax, or Notify parameter shape",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unsupported channel name, placeholder syntax, or Notify parameter shape.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10006"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS10008",
            "Internal source generator error",
            "An internal error occurred in the Postgres source generator: {0}: {1}",
            "Observables.Postgres",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the Postgres source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS10008"));
}

internal static class PostgresGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildPostgres = "BuildPostgres";
}
