using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Observables.SourceGenerators.Shared.Extensions;

internal static class INamedTypeSymbolExtensions
{
    public static string GetSafeHintName(this INamedTypeSymbol type)
    {
        string qualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
        var hint = new StringBuilder(qualifiedName.Length);
        foreach (char ch in qualifiedName)
        {
            if (ch == '.')
            {
                hint.Append('_');
            }
            else if (char.IsLetterOrDigit(ch))
            {
                hint.Append(ch);
            }
            else
            {
                // A separator is followed by an identifier, which cannot start with 0.
                hint.Append("_0").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        return hint.ToString();
    }
}
