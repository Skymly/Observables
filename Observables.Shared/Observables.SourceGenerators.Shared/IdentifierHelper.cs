using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Observables.SourceGenerators.Shared;

/// <summary>
/// Escapes C# identifiers that collide with reserved keywords by prefixing with @.
/// </summary>
internal static class IdentifierHelper
{
    /// <summary>
    /// Returns the identifier prefixed with @ if it is a C# reserved keyword; otherwise returns it unchanged.
    /// Contextual keywords (var, yield, partial, …) are valid identifiers and are not escaped.
    /// </summary>
    public static string Escape(string identifier)
    {
        var kind = SyntaxFacts.GetKeywordKind(identifier);
        return SyntaxFacts.IsKeywordKind(kind) ? "@" + identifier : identifier;
    }

    /// <summary>
    /// Returns a legal backing-field name for a member that may already be keyword-escaped.
    /// <c>event</c> and <c>@event</c> both become <c>_event</c>.
    /// </summary>
    public static string BackingFieldName(string identifier)
    {
        var unescaped = identifier.Length > 0 && identifier[0] == '@'
            ? identifier.Substring(1)
            : identifier;
        return "_" + unescaped;
    }

    /// <summary>
    /// Formats a user-supplied string as a C# string literal, including quotes.
    /// </summary>
    public static string FormatStringLiteral(string value) =>
        SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>
    /// True when <paramref name="type"/> is non-nullable <see cref="System.Threading.CancellationToken"/>,
    /// including aliases. <c>CancellationToken?</c> is not a cancellation token.
    /// </summary>
    public static bool IsCancellationToken(ITypeSymbol type)
    {
        return type.Name == "CancellationToken"
            && type.ContainingNamespace?.ToDisplayString() == "System.Threading";
    }

    /// <summary>
    /// The trailing cancellation-token parameter, or <see langword="null"/> if the last
    /// parameter is not a cancellation token.
    /// </summary>
    public static IParameterSymbol? TryGetTrailingCancellationToken(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return null;
        }

        var last = method.Parameters[method.Parameters.Length - 1];
        return IsCancellationToken(last.Type) ? last : null;
    }

    /// <summary>
    /// True when a non-trailing parameter is a cancellation token.
    /// </summary>
    public static bool HasNonTrailingCancellationToken(IMethodSymbol method)
    {
        for (var i = 0; i < method.Parameters.Length - 1; i++)
        {
            if (IsCancellationToken(method.Parameters[i].Type))
            {
                return true;
            }
        }

        return false;
    }
}
