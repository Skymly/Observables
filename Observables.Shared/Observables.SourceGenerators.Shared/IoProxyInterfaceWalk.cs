using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Shared marked-interface walk for IO stub generators.
/// Feature adapters only classify the public members collected here.
/// </summary>
internal readonly record struct MarkedInterfaceContext(
    INamedTypeSymbol InterfaceSymbol,
    InterfaceDeclarationSyntax Syntax,
    SemanticModel SemanticModel,
    Nullability Nullability,
    ImmutableArray<ISymbol> PublicInstanceMembers);

internal static class IoProxyInterfaceWalk
{
    internal static ImmutableArray<MarkedInterfaceContext> Collect(
        CSharpCompilation compilation,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        INamedTypeSymbol markerAttribute,
        CancellationToken cancellationToken)
    {
        if (candidateInterfaces.IsDefaultOrEmpty)
        {
            return ImmutableArray<MarkedInterfaceContext>.Empty;
        }

        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var builder = ImmutableArray.CreateBuilder<MarkedInterfaceContext>();
        foreach (var group in candidateInterfaces.GroupBy(static syntax => syntax.SyntaxTree))
        {
            var semanticModel = compilation.GetSemanticModel(group.Key);
            foreach (var interfaceSyntax in group)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetDeclaredSymbol(interfaceSyntax, cancellationToken) is not INamedTypeSymbol interfaceSymbol)
                {
                    continue;
                }

                if (!HasAttribute(interfaceSymbol, markerAttribute))
                {
                    continue;
                }

                // Open generics (including Outer<T>.IInner) cannot become a closed proxy (CS0246).
                // file-local interfaces cannot be referenced from generated code (CS0400).
                // OpenGenericProxyInterfaceAnalyzer reports OBS0002 for the open-generic case.
                if (IsOpenGeneric(interfaceSymbol)
                    || interfaceSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.FileKeyword)))
                {
                    continue;
                }

                if (!seen.Add(interfaceSymbol))
                {
                    continue;
                }

                var nullability = semanticModel.GetNullableContext(interfaceSyntax.SpanStart).HasFlag(NullableContext.Enabled)
                    ? Nullability.Enabled
                    : Nullability.Disabled;

                builder.Add(
                    new MarkedInterfaceContext(
                        interfaceSymbol,
                        interfaceSyntax,
                        semanticModel,
                        nullability,
                        CollectPublicInstanceMembers(interfaceSymbol)));
            }
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<ISymbol> CollectPublicInstanceMembers(INamedTypeSymbol interfaceSymbol)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var members = ImmutableArray.CreateBuilder<ISymbol>();
        AddPublicInstanceMembers(interfaceSymbol, seen, members);
        foreach (var inherited in interfaceSymbol.AllInterfaces)
        {
            AddPublicInstanceMembers(inherited, seen, members);
        }

        return members.ToImmutable();
    }

    static void AddPublicInstanceMembers(
        INamedTypeSymbol type,
        HashSet<string> seen,
        ImmutableArray<ISymbol>.Builder members)
    {
        foreach (var member in type.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
            {
                continue;
            }

            if (member is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IEventSymbol))
            {
                continue;
            }

            if (seen.Add(MemberSignature(member)))
            {
                members.Add(member);
            }
        }
    }

    static string MemberSignature(ISymbol member)
    {
        if (member is IMethodSymbol method)
        {
            var parameters = string.Join(
                ",",
                method.Parameters.Select(static p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return "M:" + method.Name + "(" + parameters + ")";
        }

        if (member is IPropertySymbol property)
        {
            return "P:" + property.Name;
        }

        return "E:" + member.Name;
    }

    internal static bool IsOpenGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }
}
