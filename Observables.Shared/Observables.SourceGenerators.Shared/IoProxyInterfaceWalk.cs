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

                var nullability = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || semanticModel.GetNullableContext(interfaceSyntax.SpanStart) == NullableContext.Enabled
                        ? Nullability.Enabled
                        : Nullability.Disabled;

                var members = interfaceSymbol.GetMembers()
                    .Where(static member => member.DeclaredAccessibility == Accessibility.Public && !member.IsStatic)
                    .ToImmutableArray();

                builder.Add(
                    new MarkedInterfaceContext(
                        interfaceSymbol,
                        interfaceSyntax,
                        semanticModel,
                        nullability,
                        members));
            }
        }

        return builder.ToImmutable();
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
