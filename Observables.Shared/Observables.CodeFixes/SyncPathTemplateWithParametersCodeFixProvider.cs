using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncPathTemplateWithParametersCodeFixProvider)), Shared]
public sealed class SyncPathTemplateWithParametersCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ProxyDomainTable.RestApi.MemberShapeMismatchDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == ProxyDomainTable.RestApi.MemberShapeMismatchDiagnosticId);
        if (diagnostic?.Location is not { IsInSource: true } location)
        {
            return;
        }

        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var method = CodeFixSyntaxHelper.FindEnclosingMethod(root, location);
        var httpAttribute = method is null ? null : CodeFixSyntaxHelper.FindHttpMethodAttributeWithLiteralPath(method);
        if (method is null || httpAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal)
        {
            return;
        }

        var currentPath = literal.Token.ValueText;
        var parameterNames = CodeFixSyntaxHelper.GetNonCancellationParameterNames(method);
        var syncedPath = PathTemplateSync.SyncPathWithParameters(currentPath, parameterNames);
        if (string.Equals(currentPath, syncedPath, StringComparison.Ordinal))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Sync path template to '{syncedPath}'",
                createChangedSolution: cancellationToken =>
                    ApplyAsync(document, method, httpAttribute, literal, syncedPath, cancellationToken),
                equivalenceKey: nameof(SyncPathTemplateWithParametersCodeFixProvider)),
            diagnostic);
    }

    static async Task<Solution> ApplyAsync(
        Document document,
        MethodDeclarationSyntax method,
        AttributeSyntax httpAttribute,
        LiteralExpressionSyntax literal,
        string syncedPath,
        CancellationToken cancellationToken)
    {
        var newLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(syncedPath));
        var newAttribute = httpAttribute.ReplaceNode(literal, newLiteral);
        var newMethod = method.ReplaceNode(httpAttribute, newAttribute);

        var solution = await CodeFixSyntaxHelper.TryApplyDocumentEditAsync(
            document,
            (editor, _) =>
            {
                editor.ReplaceNode(method, newMethod);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return solution ?? document.Project.Solution;
    }
}
