using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Shared proxy-model assembly for IO stub generators.
/// Feature parsers keep member classification (<c>TryAdd*</c>) as adapters.
/// </summary>
internal static class IoProxyModelAssembly
{
    internal static string GeneratedProxyClassName(INamedTypeSymbol iface) =>
        $"{iface.Name.TrimStart('I')}GeneratedProxy";

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
        Action<MarkedInterfaceContext>? onMarkedInterface = null)
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
