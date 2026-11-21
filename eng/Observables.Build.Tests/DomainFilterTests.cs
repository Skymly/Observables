namespace Observables.Build.Tests;

public sealed class DomainFilterTests
{
    private static readonly string[] Entries = ["mqtt", "shared", "restapi", "redis"];

    [Fact]
    public void Select_accepts_comma_separated_and_separate_arguments()
    {
        string[] selected = Select("MQTT, shared", "redis", "mqtt");

        Assert.Equal(["mqtt", "shared", "redis"], selected);
    }

    [Fact]
    public void Select_returns_all_entries_without_arguments()
    {
        Assert.Equal(Entries, Select());
    }

    [Theory]
    [InlineData("mqtt,missing", "missing")]
    [InlineData("missing", "missing")]
    [InlineData("mqtt,", "")]
    public void Select_rejects_every_unmatched_domain(string argument, string unmatched)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Select(argument));

        Assert.Contains($"no entries match domain '{unmatched}'", error.Message, StringComparison.Ordinal);
    }

    private static string[] Select(params string[] arguments) =>
        DomainFilter.Select(Entries, arguments, "--test-domains", (entry, domain) =>
            entry.Equals(domain, StringComparison.OrdinalIgnoreCase));
}
