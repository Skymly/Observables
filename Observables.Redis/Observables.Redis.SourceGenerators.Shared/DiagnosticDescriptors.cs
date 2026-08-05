using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared;

namespace Observables.Redis.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor InvalidRedisMember =
        new(
            "OBS11001",
            "Redis interface members must declare a Redis boundary attribute",
            "Member {0}.{1} has no RedisPublish or RedisSubscribe attribute, or uses a non-literal Channel template",
            "Observables.Redis",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Redis member missing boundary attribute or non-literal Channel template.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11001"));

    public static readonly DiagnosticDescriptor RedisCoreNotReferenced =
        new(
            "OBS11002",
            "Observables.Redis must be referenced",
            "Observables.Redis is not referenced. Add a PackageReference to Observables.Redis.",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Observables.Redis runtime package is not referenced.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11002"));

    public static readonly DiagnosticDescriptor UnsupportedReturnType =
        new(
            "OBS11003",
            "Unsupported return type",
            "Return type '{0}' is not supported by Observables.Redis",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Return type is not supported on a Redis member.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11003"));

    public static readonly DiagnosticDescriptor MemberShapeMismatch =
        new(
            "OBS11004",
            "Member shape mismatch for Redis boundary",
            "Member '{0}' does not match its Redis boundary attribute (methods vs properties)",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Member shape does not match the Redis boundary attribute (for example, [RedisSubscribe] on a method).",
            helpLinkUri: DiagnosticHelpLink.For("OBS11004"));

    public static readonly DiagnosticDescriptor SystemReactiveNotReferenced =
        new(
            "OBS11005",
            "SystemReactive package required for IObservable",
            "Return type '{0}' requires PackageReference to Observables.Redis.Reactive",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "IObservable<T> return type requires the Observables.Redis.Reactive package.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11005"));

    public static readonly DiagnosticDescriptor UnsupportedRedisOption =
        new(
            "OBS11006",
            "Unsupported Redis option or payload shape",
            "Member '{0}.{1}' uses an unsupported Channel template, Pattern metacharacters on Publish, extra parameters, or Subscribe placeholder syntax",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unsupported Channel template, Publish Pattern metacharacters, extra parameters, or Subscribe placeholder syntax.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11006"));

    public static readonly DiagnosticDescriptor InternalGeneratorError =
        new(
            "OBS11008",
            "Internal source generator error",
            "An internal error occurred in the Redis source generator: {0}: {1}",
            "Observables.Redis",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Unexpected internal failure in the Redis source generator.",
            helpLinkUri: DiagnosticHelpLink.For("OBS11008"));
}

internal static class RedisGeneratorStepName
{
    public const string ReportDiagnostics = "ReportDiagnostics";
    public const string BuildRedis = "BuildRedis";
}
