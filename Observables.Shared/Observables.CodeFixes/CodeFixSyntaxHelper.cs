using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Observables.CodeFixes;

internal static class CodeFixSyntaxHelper
{
    internal static async Task<Solution?> TryApplyDocumentEditAsync(
        Document document,
        Func<DocumentEditor, CancellationToken, Task> editAsync,
        CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        await editAsync(editor, cancellationToken).ConfigureAwait(false);
        return editor.GetChangedDocument().Project.Solution;
    }

    internal static MemberDeclarationSyntax? FindMemberDeclaration(SyntaxNode root, Location location)
    {
        var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        return node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
    }

    internal static MethodDeclarationSyntax? FindEnclosingMethod(SyntaxNode root, Location location) =>
        FindMemberDeclaration(root, location) as MethodDeclarationSyntax;

    internal static PropertyDeclarationSyntax? FindEnclosingProperty(SyntaxNode root, Location location) =>
        FindMemberDeclaration(root, location) as PropertyDeclarationSyntax;

    internal static bool IsCancellationTokenParameter(ParameterSyntax parameter) =>
        parameter.Type?.ToString() is "CancellationToken" or "System.Threading.CancellationToken";

    internal static IReadOnlyList<string> GetNonCancellationParameterNames(MethodDeclarationSyntax method) =>
        method.ParameterList.Parameters
            .Where(p => !IsCancellationTokenParameter(p))
            .Select(p => p.Identifier.Text)
            .ToArray();

    internal static AttributeSyntax? FindHttpMethodAttributeWithLiteralPath(MethodDeclarationSyntax method)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
        {
            if (attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax
                || !attribute.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            if (IsHttpMethodAttributeName(attribute.Name.ToString()))
            {
                return attribute;
            }
        }

        return null;
    }

    internal static bool HasHttpMethodAttribute(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => IsHttpMethodAttributeName(attribute.Name.ToString()));

    internal static AttributeSyntax? FindBoundaryAttribute(MemberDeclarationSyntax member)
    {
        foreach (var attribute in member.AttributeLists.SelectMany(list => list.Attributes))
        {
            var name = NormalizeAttributeName(attribute.Name.ToString());
            if (BoundaryAttributeDefaults.RequiresProperty(name)
                || BoundaryAttributeDefaults.RequiresMethod(name))
            {
                return attribute;
            }
        }

        return null;
    }

    internal static bool IsHttpMethodAttributeName(string name) =>
        name is "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "Options"
            or "GetAttribute" or "PostAttribute" or "PutAttribute" or "DeleteAttribute"
            or "PatchAttribute" or "HeadAttribute" or "OptionsAttribute";

    internal static string NormalizeAttributeName(string name) =>
        name.EndsWith("Attribute", StringComparison.Ordinal) ? name : name + "Attribute";

    internal static string SuggestRestApiPath(MethodDeclarationSyntax method)
    {
        var parameters = GetNonCancellationParameterNames(method);
        if (parameters.Count == 0)
        {
            return "/" + method.Identifier.Text.ToLowerInvariant();
        }

        return "/" + string.Join("/", parameters.Select(p => "{" + p + "}"));
    }

    internal static AttributeListSyntax CreateAttributeList(string attributeSource)
    {
        var member = ParseMemberDeclaration(attributeSource + " void M();") as MethodDeclarationSyntax
            ?? throw new InvalidOperationException("Unable to parse attribute list.");
        return member.AttributeLists[0];
    }

    internal static MethodDeclarationSyntax ConvertPropertyToMethod(PropertyDeclarationSyntax property) =>
        MethodDeclaration(property.Type!, property.Identifier)
            .WithAttributeLists(property.AttributeLists)
            .WithModifiers(property.Modifiers)
            .WithParameterList(ParameterList())
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            .WithLeadingTrivia(property.GetLeadingTrivia())
            .WithTrailingTrivia(property.GetTrailingTrivia());

    internal static PropertyDeclarationSyntax ConvertMethodToProperty(MethodDeclarationSyntax method) =>
        PropertyDeclaration(method.ReturnType!, method.Identifier)
            .WithAttributeLists(method.AttributeLists)
            .WithModifiers(method.Modifiers)
            .WithAccessorList(
                AccessorList(
                    SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))))
            .WithLeadingTrivia(method.GetLeadingTrivia())
            .WithTrailingTrivia(method.GetTrailingTrivia());
}
