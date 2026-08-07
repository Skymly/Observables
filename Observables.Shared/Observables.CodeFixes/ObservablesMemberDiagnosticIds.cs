using System.Collections.Immutable;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

internal static class ObservablesMemberDiagnosticIds
{
    internal static readonly ImmutableArray<string> MissingHttpMethod = ["OBS3001"];

    internal static readonly ImmutableArray<string> PathParameterMismatch = ["OBS3004"];

    internal static readonly ImmutableArray<string> MissingBoundaryAttribute =
        ProxyDomainTable.MissingBoundaryDiagnosticIds;

    internal static readonly ImmutableArray<string> MemberShapeMismatch =
        ProxyDomainTable.MemberShapeMismatchDiagnosticIds;

    /// <summary>
    /// Alias of <see cref="ProxyDomainTable.DomainKind"/> for CodeFix call sites / tests.
    /// RestAPI is intentionally omitted — it uses HTTP-specific fixes.
    /// </summary>
    internal enum InterfaceProxyDomain
    {
        SignalR = ProxyDomainTable.DomainKind.SignalR,
        Mqtt = ProxyDomainTable.DomainKind.Mqtt,
        WebSocket = ProxyDomainTable.DomainKind.WebSocket,
        Grpc = ProxyDomainTable.DomainKind.Grpc,
        Sse = ProxyDomainTable.DomainKind.Sse,
        Nats = ProxyDomainTable.DomainKind.Nats,
        Postgres = ProxyDomainTable.DomainKind.Postgres,
        Redis = ProxyDomainTable.DomainKind.Redis,
    }

    internal static bool TryGetDomain(string diagnosticId, out InterfaceProxyDomain domain)
    {
        if (ProxyDomainTable.TryGetByDiagnosticId(diagnosticId, out var definition)
            && definition.Kind != ProxyDomainTable.DomainKind.RestApi)
        {
            domain = (InterfaceProxyDomain)definition.Kind;
            return true;
        }

        domain = default;
        return false;
    }
}
