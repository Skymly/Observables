using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Completion;

namespace Observables.Analyzers;

internal static class CompletionItemFactory
{
    private const string InsertTextPropertyName = "InsertText";

    internal static CompletionItem Create(string displayText, string insertText, string? sortText = null) =>
        CompletionItem
            .Create(
                displayText: displayText,
                sortText: sortText ?? displayText,
                rules: CompletionItemRules.Default)
            .AddProperty(InsertTextPropertyName, insertText);
}
