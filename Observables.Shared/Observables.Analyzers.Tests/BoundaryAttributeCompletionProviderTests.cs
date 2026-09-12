using Observables.Roslyn.Shared;

namespace Observables.Analyzers.Tests;

public sealed class BoundaryAttributeCompletionProviderTests
{
    [Fact]
    public void Commit_text_for_name_only_suggestion_has_no_trailing_bracket()
    {
        var suggestion = new ProxyDomainTable.BoundaryAttributeSuggestion("MqttSubscribe", "MqttSubscribe");
        var text = BoundaryAttributeCompletionProvider.ToCommitText(suggestion, "event");

        Assert.Equal("""MqttSubscribe("event")""", text);
        Assert.DoesNotContain("]", text);
    }

    [Fact]
    public void Commit_text_preserves_insert_text_that_already_has_parentheses()
    {
        var suggestion = new ProxyDomainTable.BoundaryAttributeSuggestion("Get", """Get("/path")""");
        var text = BoundaryAttributeCompletionProvider.ToCommitText(suggestion, "GetUser");

        Assert.Equal("""Get("/path")""", text);
        Assert.DoesNotContain("]", text);
    }
}
