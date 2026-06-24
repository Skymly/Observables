using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared.Extensions;

internal static class INamedTypeSymbolExtensions
{
    public static string GetSafeHintName(this INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');
    }
}
