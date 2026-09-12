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
}
