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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddHttpMethodAttributeCodeFixProvider)), Shared]
public sealed class AddHttpMethodAttributeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [ProxyDomainTable.RestApi.MissingBoundaryDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == ProxyDomainTable.RestApi.MissingBoundaryDiagnosticId);
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
        if (method is null || CodeFixSyntaxHelper.HasHttpMethodAttribute(method))
        {
            return;
        }

        var path = CodeFixSyntaxHelper.SuggestRestApiPath(method);
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add [Get(\"{path}\")] attribute",
                createChangedSolution: cancellationToken =>
                    ApplyAsync(document, method, path, cancellationToken),
                equivalenceKey: nameof(AddHttpMethodAttributeCodeFixProvider)),
            diagnostic);
    }

    static async Task<Solution> ApplyAsync(
        Document document,
        MethodDeclarationSyntax method,
        string path,
        CancellationToken cancellationToken)
    {
        var attributeList = CodeFixSyntaxHelper.CreateAttributeList($"[Get(\"{path}\")]");
        var solution = await CodeFixSyntaxHelper.TryApplyDocumentEditAsync(
            document,
            (editor, _) =>
            {
                var updated = method.WithAttributeLists(method.AttributeLists.Add(attributeList));
                editor.ReplaceNode(method, updated);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return solution ?? document.Project.Solution;
    }
}
