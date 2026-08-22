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

[ExportCompletionProvider(nameof(RestApiMemberCompletionProvider), LanguageNames.CSharp)]
[Shared]
public sealed class RestApiMemberCompletionProvider : CompletionProvider
{
    public override bool ShouldTriggerCompletion(
        SourceText text,
        int caretPosition,
        CompletionTrigger trigger,
        OptionSet options)
    {
        if (trigger.Kind == CompletionTriggerKind.Insertion && caretPosition > 0)
        {
            var ch = text[caretPosition - 1];
            return ch is '[' or '"';
        }

        return trigger.Kind == CompletionTriggerKind.Invoke;
    }

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

        var method = root.FindToken(position).Parent?
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (method is null)
        {
            return;
        }

        if (IsInsidePathLiteral(root, position, method, out _))
        {
            foreach (var item in CreatePathPlaceholderItems(method))
            {
                context.AddItem(item);
            }

            return;
        }

        if (root.FindToken(position - 1).Parent is AttributeListSyntax)
        {
            foreach (var item in CreateHttpMethodItems(method))
            {
                context.AddItem(item);
            }
        }
    }

    static IEnumerable<CompletionItem> CreateHttpMethodItems(MethodDeclarationSyntax method)
    {
        var path = RestApiPathSuggestions.SuggestPath(method);
        foreach (var verb in ProxyDomainTable.RestApiHttpMethodNames)
        {
            yield return CompletionItemFactory.Create(verb, $"{verb}(\"{path}\")]");
        }
    }

    static IEnumerable<CompletionItem> CreatePathPlaceholderItems(MethodDeclarationSyntax method)
    {
        foreach (var name in RestApiPathSuggestions.GetNonCancellationParameterNames(method))
        {
            yield return CompletionItemFactory.Create($"{{{name}}}", $"{{{name}}}", name);
        }
    }

    static bool IsInsidePathLiteral(
        SyntaxNode root,
        int position,
        MethodDeclarationSyntax method,
        out TextSpan literalSpan)
    {
        literalSpan = default;
        foreach (var attributeList in method.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!IsHttpMethodAttributeName(attribute.Name.ToString()))
                {
                    continue;
                }

                var firstArg = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                if (firstArg is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
                    && literal.Span.Contains(position))
                {
                    literalSpan = literal.Span;
                    return true;
                }
            }
        }

        return false;
    }

    static bool IsHttpMethodAttributeName(string name) =>
        ProxyDomainTable.RestApiHttpMethodNames.Any(verb =>
            name is var n && (n == verb || n == verb + "Attribute"));
}
