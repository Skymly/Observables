using Microsoft.CodeAnalysis;

namespace Observables.Analyzers;

internal static class ReactivePackageConflictDetection
{
    internal static bool HasAssemblyReference(Compilation compilation, string assemblyName)
    {
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                && string.Equals(assembly.Name, assemblyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasR3Reference(Compilation compilation) =>
        HasAssemblyReference(compilation, "R3");

    internal static bool HasReactiveBridgeReference(Compilation compilation, ProxyDomainCatalog.ProxyDomain domain) =>
        domain switch
        {
            { DisplayName: "SignalR" } => HasAssemblyReference(compilation, "Observables.SignalR.Reactive"),
            { DisplayName: "Mqtt" } => HasAssemblyReference(compilation, "Observables.Mqtt.Reactive"),
            { DisplayName: "WebSocket" } => HasAssemblyReference(compilation, "Observables.WebSocket.Reactive"),
            { DisplayName: "RestAPI" } => HasAssemblyReference(compilation, "Observables.RestAPI.Reactive"),
            _ => false,
        };
}
