namespace Observables.CodeFixes.Tests;

public sealed class PathTemplateSyncTests
{
    [Fact]
    public void SyncPathWithParameters_appends_missing_placeholder()
    {
        var synced = PathTemplateSync.SyncPathWithParameters(
            "/users/{id}",
            ["id", "page"]);

        Assert.Equal("/users/{id}/{page}", synced);
    }

    [Fact]
    public void SyncPathWithParameters_removes_extra_placeholder()
    {
        var synced = PathTemplateSync.SyncPathWithParameters(
            "/users/{id}/{page}",
            ["id"]);

        Assert.Equal("/users/{id}", synced);
    }

    [Fact]
    public void SyncPathWithParameters_is_idempotent_when_already_synced()
    {
        const string path = "/users/{id}/{page}";
        var synced = PathTemplateSync.SyncPathWithParameters(path, ["id", "page"]);
        Assert.Equal(path, synced);
    }
}
