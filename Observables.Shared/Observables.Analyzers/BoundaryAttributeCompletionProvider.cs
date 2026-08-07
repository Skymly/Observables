using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Observables.Roslyn.Shared;

namespace Observables.Analyzers;

[ExportCompletionProvider(nameof(BoundaryAttributeCompletionProvider), LanguageNames.CSharp)]
[Shared]
public sealed class BoundaryAttributeCompletionProvider : CompletionProvider
{
    public override bool ShouldTriggerCompletion(
        SourceText text,
        int caretPosition,
        CompletionTrigger trigger,
        OptionSet options) =>
        trigger.Kind == CompletionTriggerKind.Insertion
        && caretPosition > 0
        && text[caretPosition - 1] == '[';

    public override async Task ProvideCompletionsAsync(CompletionContext context)
    {
        var document = context.Document;
        var position = context.Position;
        var cancellationToken = context.CancellationToken;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        if (root.FindToken(position - 1).Parent is not AttributeListSyntax)
        {
            return;
        }

        var member = root.FindToken(position).Parent?
            .AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault();
        if (member is null)
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var interfaceSyntax = member.Ancestors().OfType<InterfaceDeclarationSyntax>().FirstOrDefault();
        if (interfaceSyntax is null
            || semanticModel.GetDeclaredSymbol(interfaceSyntax, cancellationToken) is not INamedTypeSymbol interfaceSymbol)
        {
            return;
        }

        var domain = ProxyDomainCatalog.TryGetInterfaceProxyDomain(interfaceSymbol, semanticModel.Compilation);
        if (domain is null)
        {
            return;
        }

        var suggestions = member switch
        {
            MethodDeclarationSyntax => domain.MethodAttributes,
            PropertyDeclarationSyntax => domain.PropertyAttributes,
            _ => null,
        };

        if (suggestions is null || suggestions.Count == 0)
        {
            return;
        }

        var memberName = member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            _ => "Member",
        };

        foreach (var suggestion in suggestions)
        {
            context.AddItem(CreateItem(suggestion, memberName));
        }
    }

    static CompletionItem CreateItem(ProxyDomainTable.BoundaryAttributeSuggestion suggestion, string memberName)
    {
        var insertText = suggestion.InsertText.Contains('(')
            ? suggestion.InsertText
            : $"{suggestion.InsertText}(\"{memberName}\")]";

        return CompletionItemFactory.Create(suggestion.DisplayText, insertText);
    }
}
