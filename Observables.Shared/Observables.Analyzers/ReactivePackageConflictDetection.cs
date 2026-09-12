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

    internal static bool HasReactiveBridgeReference(Compilation compilation, ProxyDomainCatalog.ProxyDomain domain) =>
        HasAssemblyReference(compilation, domain.Definition.ReactiveAssemblyName);

    /// <summary>
    /// True when this compilation is using the Observables.&lt;Feature&gt;.R3 generator
    /// (assembly reference or generated <c>Observables.&lt;Feature&gt;.Generated</c> namespace).
    /// Independent references to the R3 library must not count.
    /// </summary>
    internal static bool HasObservablesR3Backend(Compilation compilation, ProxyDomainCatalog.ProxyDomain domain)
    {
        var displayName = domain.Definition.DisplayName;
        if (HasAssemblyReference(compilation, $"Observables.{displayName}.R3")
            || HasAssemblyReference(compilation, $"Observables.{displayName}.R3.SourceGenerators"))
        {
            return true;
        }

        return HasNamespace(compilation.GlobalNamespace, $"Observables.{displayName}.Generated");
    }

    internal static bool HasNamespace(INamespaceSymbol root, string dottedName)
    {
        var current = root;
        foreach (var part in dottedName.Split('.'))
        {
            INamespaceSymbol? next = null;
            foreach (var child in current.GetNamespaceMembers())
            {
                if (string.Equals(child.Name, part, StringComparison.Ordinal))
                {
                    next = child;
                    break;
                }
            }

            if (next is null)
            {
                return false;
            }

            current = next;
        }

        return true;
    }
}
