using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddBoundaryAttributeCodeFixProvider)), Shared]
public sealed class AddBoundaryAttributeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ObservablesMemberDiagnosticIds.MissingBoundaryAttribute;

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null
            || !ObservablesMemberDiagnosticIds.TryGetDomain(diagnostic.Id, out var domain)
            || diagnostic.Location is not { IsInSource: true } location)
        {
            return;
        }

        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var member = CodeFixSyntaxHelper.FindMemberDeclaration(root, location);
        if (member is null || CodeFixSyntaxHelper.FindBoundaryAttribute(member) is not null)
        {
            return;
        }

        var attributeSource = member switch
        {
            MethodDeclarationSyntax method =>
                BoundaryAttributeDefaults.MethodAttribute(domain, method.Identifier.Text),
            PropertyDeclarationSyntax property =>
                BoundaryAttributeDefaults.PropertyAttribute(domain, property.Identifier.Text),
            _ => null,
        };

        if (attributeSource is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add {attributeSource} attribute",
                createChangedSolution: cancellationToken =>
                    ApplyAsync(document, member, attributeSource, cancellationToken),
                equivalenceKey: $"{nameof(AddBoundaryAttributeCodeFixProvider)}:{diagnostic.Id}"),
            diagnostic);
    }

    static async Task<Solution> ApplyAsync(
        Document document,
        MemberDeclarationSyntax member,
        string attributeSource,
        CancellationToken cancellationToken)
    {
        var attributeList = CodeFixSyntaxHelper.CreateAttributeList(attributeSource);
        var solution = await CodeFixSyntaxHelper.TryApplyDocumentEditAsync(
            document,
            (editor, _) =>
            {
                var updated = member.WithAttributeLists(member.AttributeLists.Add(attributeList));
                editor.ReplaceNode(member, updated);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return solution ?? document.Project.Solution;
    }
}
