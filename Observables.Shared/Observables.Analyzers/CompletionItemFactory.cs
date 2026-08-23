using Microsoft.CodeAnalysis.Completion;

namespace Observables.Analyzers;

/// <summary>
/// Builds <see cref="CompletionItem"/>s whose <see cref="CompletionItem.DisplayText"/>
/// is the exact text that will be inserted on commit (Roslyn's default commit inserts DisplayText).
/// Callers MUST NOT append a trailing <c>]</c>: the editor auto-closes the bracket the user typed.
/// </summary>
internal static class CompletionItemFactory
{
    internal static CompletionItem Create(string displayAndInsertText, string? sortText = null) =>
        CompletionItem
            .Create(
                displayText: displayAndInsertText,
                sortText: sortText ?? displayAndInsertText,
                rules: CompletionItemRules.Default);
}
