using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Observables.Roslyn.Shared;

namespace Observables.Analyzers;

internal static class ReactivePackageConflictDetection
{
    /// <summary>
    /// Backend presence signals for one domain: the package-declared
    /// <c>build_property.Observables&lt;Feature&gt;Backend_&lt;Backend&gt;</c> key (primary), or the
    /// dev-time symbol fallback — a referenced <c>Observables.&lt;Feature&gt;.&lt;Backend&gt;</c>
    /// bridge assembly or a generator-emitted marker type — for
    /// <c>ProjectReference + OutputItemType=Analyzer</c> flows where no props are imported.
    /// Independent references to the bare R3 library never count.
    /// </summary>
    internal static ImmutableArray<ProxyDomainTable.BackendKind> DetectBackends(
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        ProxyDomainTable.ProxyDomainDefinition domain)
    {
        var builder = ImmutableArray.CreateBuilder<ProxyDomainTable.BackendKind>(ProxyDomainTable.BackendKinds.Count);
        foreach (var backend in ProxyDomainTable.BackendKinds)
        {
            if (IsBackendDeclared(optionsProvider, domain, backend)
                || IsBackendPresentInSymbols(compilation, domain, backend))
            {
                builder.Add(backend);
            }
        }

        return builder.ToImmutable();
    }

    static bool IsBackendDeclared(
        AnalyzerConfigOptionsProvider optionsProvider,
        ProxyDomainTable.ProxyDomainDefinition domain,
        ProxyDomainTable.BackendKind backend) =>
        optionsProvider.GlobalOptions.TryGetValue(
            $"build_property.{domain.BackendKeyName(backend)}",
            out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    static bool IsBackendPresentInSymbols(
        Compilation compilation,
        ProxyDomainTable.ProxyDomainDefinition domain,
        ProxyDomainTable.BackendKind backend) =>
        HasAssemblyReference(compilation, domain.BackendAssemblyName(backend))
        || (domain.BackendMarkerMetadataName(backend) is { } marker
            && compilation.GetTypeByMetadataName(marker) is not null);

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
}
