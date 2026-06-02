using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Observables.SourceGenerators.Shared.Extensions;

internal static class INamedTypeSymbolExtensions
{
    public static bool IsPartial(this INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences
            .Select(static x => x.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(static x => x.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    public static string GetSafeHintName(this INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');
    }
}
