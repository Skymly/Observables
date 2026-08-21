namespace Observables.Mqtt.Tests;

public sealed class MqttTopicMatcherTests
{
    [Theory]
    [InlineData("a/#", "a")]
    [InlineData("a/#", "a/b")]
    [InlineData("a/#", "a/b/c")]
    [InlineData("#", "a")]
    [InlineData("#", "a/b")]
    [InlineData("+/b/#", "x/b")]
    [InlineData("+/b/#", "x/b/c")]
    [InlineData("sport/tennis/player1/#", "sport/tennis/player1")]
    [InlineData("sport/tennis/player1/#", "sport/tennis/player1/ranking")]
    public void Multi_level_wildcard_matches_zero_or_more_remaining_levels(string filter, string topic)
    {
        Assert.True(MqttTopicMatcher.Matches(filter, topic));
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a/b", "a/b")]
    [InlineData("a/+", "a/b")]
    [InlineData("+/+", "a/b")]
    [InlineData("a/+/c", "a/b/c")]
    [InlineData("a/+/c", "a//c")]
    public void Exact_and_single_level_filters_match(string filter, string topic)
    {
        Assert.True(MqttTopicMatcher.Matches(filter, topic));
    }

    [Theory]
    [InlineData("a/#", "b")]
    [InlineData("a/#", "b/a")]
    [InlineData("a/b/#", "a")]
    [InlineData("a/+", "a")]
    [InlineData("a/+", "a/b/c")]
    [InlineData("a/b", "a/b/c")]
    [InlineData("a/b", "a")]
    [InlineData("+/b", "a")]
    public void Filter_does_not_match_unrelated_topics(string filter, string topic)
    {
        Assert.False(MqttTopicMatcher.Matches(filter, topic));
    }

    [Fact]
    public void Null_topic_does_not_match()
    {
        Assert.False(MqttTopicMatcher.Matches("a/#", null));
    }
}
