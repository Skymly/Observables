using Observables.Redis;

namespace Observables.Redis.Tests;

public sealed class RedisChannelTemplateTests
{
    [Fact]
    public void Format_does_not_reparse_replacement_values()
    {
        var result = RedisChannelTemplate.Format(
            "cmd.{a}.{b}",
            ("a", "{b}"),
            ("b", "X"));

        Assert.Equal("cmd.{b}.X", result);
    }

    [Fact]
    public void Format_replaces_each_original_placeholder_once()
    {
        var result = RedisChannelTemplate.Format(
            "cmd.{a}.{b}",
            ("b", "Y"),
            ("a", "X"));

        Assert.Equal("cmd.X.Y", result);
    }

    [Fact]
    public void Format_leaves_unknown_placeholders_intact()
    {
        var result = RedisChannelTemplate.Format("cmd.{a}.{z}", ("a", "X"));
        Assert.Equal("cmd.X.{z}", result);
    }
}
