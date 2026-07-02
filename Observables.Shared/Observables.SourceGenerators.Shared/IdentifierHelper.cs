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
}
