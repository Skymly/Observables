using System.Collections.Immutable;

namespace Observables.CodeFixes;

internal static class ObservablesMemberDiagnosticIds
{
    internal static readonly ImmutableArray<string> MissingHttpMethod = ["OBS3001"];

    internal static readonly ImmutableArray<string> PathParameterMismatch = ["OBS3004"];

    internal static readonly ImmutableArray<string> MissingBoundaryAttribute =
        ["OBS4001", "OBS5001", "OBS6001", "OBS8001", "OBS9001", "OBS11001"];

    internal static readonly ImmutableArray<string> MemberShapeMismatch =
        ["OBS4004", "OBS5004", "OBS6004", "OBS8004", "OBS9004", "OBS11004"];

    internal enum InterfaceProxyDomain
    {
        SignalR,
        Mqtt,
        WebSocket,
        Sse,
        Nats,
        Redis,
    }

    internal static bool TryGetDomain(string diagnosticId, out InterfaceProxyDomain domain)
    {
        domain = diagnosticId switch
        {
            "OBS4001" or "OBS4004" => InterfaceProxyDomain.SignalR,
            "OBS5001" or "OBS5004" => InterfaceProxyDomain.Mqtt,
            "OBS6001" or "OBS6004" => InterfaceProxyDomain.WebSocket,
            "OBS8001" or "OBS8004" => InterfaceProxyDomain.Sse,
            "OBS9001" or "OBS9004" => InterfaceProxyDomain.Nats,
            "OBS11001" or "OBS11004" => InterfaceProxyDomain.Redis,
            _ => default,
        };

        return diagnosticId is "OBS4001" or "OBS4004" or "OBS5001" or "OBS5004" or "OBS6001" or "OBS6004"
            or "OBS8001" or "OBS8004" or "OBS9001" or "OBS9004"
            or "OBS11001" or "OBS11004";
    }
}
