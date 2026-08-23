using Observables.Analyzers;

namespace Observables.Analyzers.Tests;

public sealed class CompletionItemFactoryTests
{
    [Fact]
    public void DisplayText_equals_the_text_passed_in_and_becomes_the_inserted_text()
    {
        var item = CompletionItemFactory.Create("""HubInvoke("MethodName")""");

        Assert.Equal("""HubInvoke("MethodName")""", item.DisplayText);
        Assert.False(item.Properties.ContainsKey("InsertText"));
    }

    [Fact]
    public void DisplayText_does_not_carry_a_trailing_bracket_so_the_editor_auto_close_does_not_double_it()
    {
        var item = CompletionItemFactory.Create("""HubInvoke("MethodName")""");

        Assert.DoesNotContain("]", item.DisplayText);
    }

    [Fact]
    public void SortText_defaults_to_display_text()
    {
        var item = CompletionItemFactory.Create("""HubInvoke("MethodName")""");

        Assert.Equal(item.DisplayText, item.SortText);
    }

    [Fact]
    public void SortText_can_be_overridden()
    {
        var item = CompletionItemFactory.Create("""{id}""", sortText: """path-{id}""");

        Assert.Equal("""{id}""", item.DisplayText);
        Assert.Equal("""path-{id}""", item.SortText);
    }
}
