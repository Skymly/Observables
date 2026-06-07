using Microsoft.CodeAnalysis.CSharp;

namespace Observables.CodeFixes.Tests;

public sealed class CodeFixSyntaxHelperTests
{
    [Fact]
    public void ConvertMethodToProperty_preserves_attributes_and_return_type()
    {
        const string source =
            """
            public interface IHub
            {
                [HubOn("ReceiveMessage")]
                Observable<string> ReceiveMessage();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var method = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single();
        var property = CodeFixSyntaxHelper.ConvertMethodToProperty(method);

        var text = property.ToFullString();
        Assert.Contains("HubOn", text, StringComparison.Ordinal);
        Assert.Contains("get;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("();", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestRestApiPath_uses_parameter_placeholders()
    {
        const string source =
            """
            public interface IApi
            {
                Observable<User> GetUser(int id, int page);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var method = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single();
        var path = CodeFixSyntaxHelper.SuggestRestApiPath(method);

        Assert.Equal("/{id}/{page}", path);
    }
}
