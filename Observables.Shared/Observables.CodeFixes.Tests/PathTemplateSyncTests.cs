using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Observables.Analyzers;
using Observables.RestAPI;

namespace Observables.CodeFixes.Tests;

public sealed class PathTemplateSyncTests
{
    [Fact]
    public void Sync_appends_nothing_for_query_parameter()
    {
        var method = ParseMethod(
            """
            public interface IApi
            {
                [Get("/users/{id}/{page}")]
                void GetUser(int id, [Query] int page);
            }
            """);

        var synced = RestApiPathTemplate.Parse("/users/{id}/{page}").Sync(RestApiSyntaxSlots.From(method));
        Assert.Equal("/users/{id}", synced);
    }

    [Fact]
    public void Sync_renames_mismatched_placeholder()
    {
        var method = ParseMethod(
            """
            public interface IApi
            {
                [Get("/users/{id}")]
                void GetUser(int userId);
            }
            """);

        var synced = RestApiPathTemplate.Parse("/users/{id}").Sync(RestApiSyntaxSlots.From(method));
        Assert.Equal("/users/{userId}", synced);
    }

    [Fact]
    public void Sync_is_idempotent_when_already_matched()
    {
        var method = ParseMethod(
            """
            public interface IApi
            {
                [Get("/users/{id}")]
                void GetUser(int id, [Query] string q);
            }
            """);

        const string path = "/users/{id}";
        var synced = RestApiPathTemplate.Parse(path).Sync(RestApiSyntaxSlots.From(method));
        Assert.Equal(path, synced);
    }

    static MethodDeclarationSyntax ParseMethod(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        return tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
    }
}
