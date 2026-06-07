using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixMemberShapeCodeFixProvider)), Shared]
public sealed class FixMemberShapeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ObservablesMemberDiagnosticIds.MemberShapeMismatch;

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
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

        var member = CodeFixSyntaxHelper.FindMemberDeclaration(root, location);
        var boundaryAttribute = member is null ? null : CodeFixSyntaxHelper.FindBoundaryAttribute(member);
        if (member is null || boundaryAttribute is null)
        {
            return;
        }

        var attributeName = CodeFixSyntaxHelper.NormalizeAttributeName(boundaryAttribute.Name.ToString());
        switch (member)
        {
            case MethodDeclarationSyntax method when BoundaryAttributeDefaults.RequiresProperty(attributeName):
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Convert method to property",
                        createChangedSolution: cancellationToken =>
                            ApplyMethodToPropertyAsync(document, method, cancellationToken),
                        equivalenceKey: $"{nameof(FixMemberShapeCodeFixProvider)}:MethodToProperty"),
                    diagnostic);
                break;

            case PropertyDeclarationSyntax property when BoundaryAttributeDefaults.RequiresMethod(attributeName):
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Convert property to method",
                        createChangedSolution: cancellationToken =>
                            ApplyPropertyToMethodAsync(document, property, cancellationToken),
                        equivalenceKey: $"{nameof(FixMemberShapeCodeFixProvider)}:PropertyToMethod"),
                    diagnostic);
                break;
        }
    }

    static async Task<Solution> ApplyMethodToPropertyAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var property = CodeFixSyntaxHelper.ConvertMethodToProperty(method);
        var solution = await CodeFixSyntaxHelper.TryApplyDocumentEditAsync(
            document,
            (editor, _) =>
            {
                editor.ReplaceNode(method, property);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return solution ?? document.Project.Solution;
    }

    static async Task<Solution> ApplyPropertyToMethodAsync(
        Document document,
        PropertyDeclarationSyntax property,
        CancellationToken cancellationToken)
    {
        var method = CodeFixSyntaxHelper.ConvertPropertyToMethod(property);
        var solution = await CodeFixSyntaxHelper.TryApplyDocumentEditAsync(
            document,
            (editor, _) =>
            {
                editor.ReplaceNode(property, method);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return solution ?? document.Project.Solution;
    }
}
