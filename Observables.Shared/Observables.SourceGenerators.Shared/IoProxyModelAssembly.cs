using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.SourceGenerators.Shared.Extensions;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Shared proxy-model assembly for IO stub generators.
/// Feature parsers keep member classification (<c>TryAdd*</c>) as adapters.
/// </summary>
internal static class IoProxyModelAssembly
{
    internal static string GeneratedProxyClassName(INamedTypeSymbol iface)
    {
        var name = iface.Name;
        if (name.Length >= 2 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        if (iface.Arity > 0)
        {
            name += iface.Arity.ToString();
        }

        var prefix = ContainingIdentityPrefix(iface);
        return prefix.Length == 0 ? name + "GeneratedProxy" : prefix + "_" + name + "GeneratedProxy";
    }

    static string ContainingIdentityPrefix(INamedTypeSymbol type)
    {
        var nested = string.Empty;
        var containing = type.ContainingType;
        while (containing is not null)
        {
            nested = nested.Length == 0 ? containing.Name : containing.Name + "_" + nested;
            containing = containing.ContainingType;
        }

        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            var nsPrefix = ns.ToDisplayString().Replace(".", "_");
            return nested.Length == 0 ? nsPrefix : nsPrefix + "_" + nested;
        }

        return nested;
    }

    internal static string GeneratedHintName(INamedTypeSymbol iface, string domainSuffix) =>
        $"{iface.GetSafeHintName()}.{domainSuffix}.g.cs";

    internal static (List<Diagnostic> diagnostics, TContextModel model) Parse<TMember, TInterfaceModel, TContextModel>(
        ImmutableArray<MarkedInterfaceContext> markedInterfaces,
        CancellationToken cancellationToken,
        bool coreReferenced,
        DiagnosticDescriptor coreNotReferenced,
        Func<TContextModel> emptyModel,
        Action<MarkedInterfaceContext, IMethodSymbol, List<TMember>, List<Diagnostic>> tryAddMethod,
        Action<MarkedInterfaceContext, IPropertySymbol, List<TMember>, List<Diagnostic>>? tryAddProperty,
        Func<MarkedInterfaceContext, string, ImmutableEquatableArray<TMember>, TInterfaceModel> createInterface,
        Func<ImmutableEquatableArray<TInterfaceModel>, TContextModel> createContext,
        Action<MarkedInterfaceContext>? onMarkedInterface = null,
        Action<MarkedInterfaceContext, ISymbol, List<TMember>, List<Diagnostic>>? tryAddOther = null)
        where TMember : IEquatable<TMember>
        where TInterfaceModel : IEquatable<TInterfaceModel>
    {
        var diagnostics = new List<Diagnostic>();
        if (!coreReferenced)
        {
            diagnostics.Add(Diagnostic.Create(coreNotReferenced, null));
            return (diagnostics, emptyModel());
        }

        var interfaces = new List<TInterfaceModel>();
        foreach (var marked in markedInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (marked.InterfaceSymbol.TypeParameters.Length > 0)
            {
                continue;
            }

            onMarkedInterface?.Invoke(marked);
            var members = new List<TMember>();
            foreach (var member in marked.PublicInstanceMembers)
            {
                switch (member)
                {
                    case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
                        tryAddMethod(marked, method, members, diagnostics);
                        break;
                    case IPropertySymbol property:
                        tryAddProperty?.Invoke(marked, property, members, diagnostics);
                        break;
                    default:
                        tryAddOther?.Invoke(marked, member, members, diagnostics);
                        break;
                }
            }

            if (members.Count == 0)
            {
                continue;
            }

            interfaces.Add(
                createInterface(
                    marked,
                    GeneratedProxyClassName(marked.InterfaceSymbol),
                    members.ToImmutableEquatableArray()));
        }

        return (diagnostics, createContext(interfaces.ToImmutableEquatableArray()));
    }
}
