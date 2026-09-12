namespace Observables.RestAPI.Tests;

public sealed class RestApiPathTemplateTests
{
    [Fact]
    public void Query_param_is_not_a_path_placeholder()
    {
        var path = RestApiPathTemplate.Parse("/users/{id}");
        var slots = new RestApiParameterSlot[]
        {
            new("id", RestApiDeclaredKind.None),
            new("q", RestApiDeclaredKind.Query),
            new("ct", RestApiDeclaredKind.Cancellation),
        };

        Assert.True(path.Matches(slots));
        Assert.Equal("/users/{id}", path.Sync(slots));
    }

    [Fact]
    public void Body_param_is_not_required_in_path()
    {
        var path = RestApiPathTemplate.Parse("/repos/{owner}/{repo}/issues/{number}/comments");
        var slots = new RestApiParameterSlot[]
        {
            new("owner", RestApiDeclaredKind.None),
            new("repo", RestApiDeclaredKind.None),
            new("number", RestApiDeclaredKind.None),
            new("body", RestApiDeclaredKind.Body),
        };

        Assert.True(path.Matches(slots));
    }

    [Fact]
    public void Placeholder_name_mismatch_does_not_match()
    {
        var path = RestApiPathTemplate.Parse("/users/{id}");
        var slots = new RestApiParameterSlot[]
        {
            new("userId", RestApiDeclaredKind.None),
        };

        Assert.False(path.Matches(slots));
    }

    [Fact]
    public void Sync_renames_single_unresolved_placeholder()
    {
        var path = RestApiPathTemplate.Parse("/users/{id}");
        var slots = new RestApiParameterSlot[]
        {
            new("userId", RestApiDeclaredKind.None),
        };

        Assert.Equal("/users/{userId}", path.Sync(slots));
    }

    [Fact]
    public void Sync_removes_query_placeholder()
    {
        var path = RestApiPathTemplate.Parse("/users/{id}/{page}");
        var slots = new RestApiParameterSlot[]
        {
            new("id", RestApiDeclaredKind.None),
            new("page", RestApiDeclaredKind.Query),
        };

        Assert.False(path.Matches(slots));
        Assert.Equal("/users/{id}", path.Sync(slots));
    }

    [Fact]
    public void Suggest_excludes_body_and_query()
    {
        var slots = new RestApiParameterSlot[]
        {
            new("dto", RestApiDeclaredKind.Body),
        };

        Assert.Equal("/create", RestApiPathTemplate.Suggest("Create", slots));
    }

    [Fact]
    public void Suggest_uses_unmarked_parameters()
    {
        var slots = new RestApiParameterSlot[]
        {
            new("id", RestApiDeclaredKind.None),
            new("name", RestApiDeclaredKind.None),
        };

        Assert.Equal("/{id}/{name}", RestApiPathTemplate.Suggest("GetUser", slots));
    }
}
