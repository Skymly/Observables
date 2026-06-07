using System.Collections.Immutable;

namespace Observables.CodeFixes;

internal static class ObservablesMemberDiagnosticIds
{
    internal static readonly ImmutableArray<string> MissingHttpMethod = ["OBS3001"];

    internal static readonly ImmutableArray<string> PathParameterMismatch = ["OBS3004"];

    internal static readonly ImmutableArray<string> MissingBoundaryAttribute =
        ["OBS4001", "OBS5001", "OBS6001"];

    internal static readonly ImmutableArray<string> MemberShapeMismatch =
        ["OBS4004", "OBS5004", "OBS6004"];

    internal enum InterfaceProxyDomain
    {
        SignalR,
        Mqtt,
        WebSocket,
    }

    internal static bool TryGetDomain(string diagnosticId, out InterfaceProxyDomain domain)
    {
        domain = diagnosticId switch
        {
            "OBS4001" or "OBS4004" => InterfaceProxyDomain.SignalR,
            "OBS5001" or "OBS5004" => InterfaceProxyDomain.Mqtt,
            "OBS6001" or "OBS6004" => InterfaceProxyDomain.WebSocket,
            _ => default,
        };

        return diagnosticId is "OBS4001" or "OBS4004" or "OBS5001" or "OBS5004" or "OBS6001" or "OBS6004";
    }
}
