using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Observables.Analyzers.Tests;

public sealed class RestApiPathSuggestionsTests
{
    [Fact]
    public void SuggestPath_uses_parameter_placeholders()
    {
        const string source =
            """
            public interface IApi
            {
                void GetUser(int id, string name);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var path = RestApiPathSuggestions.SuggestPath(method);

        Assert.Equal("/{id}/{name}", path);
    }

    [Fact]
    public void SuggestPath_uses_method_name_when_parameterless()
    {
        const string source =
            """
            public interface IApi
            {
                void GetUsers();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var path = RestApiPathSuggestions.SuggestPath(method);

        Assert.Equal("/getusers", path);
    }
}
