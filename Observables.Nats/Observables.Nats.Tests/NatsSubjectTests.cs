namespace Observables.Nats.Tests;

public sealed class NatsSubjectTests
{
    [Fact]
    public void Format_keeps_a_literal_token()
    {
        Assert.Equal("orders.42.cancel", NatsSubject.Format("orders.{id}.cancel", ("id", "42")));
    }

    [Fact]
    public void Format_rejects_a_value_that_spans_tokens()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => NatsSubject.Format("orders.{tenant}.cancel", ("tenant", "acme.prod")));
        Assert.Contains("single subject token", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*")]
    [InlineData(">")]
    [InlineData("")]
    [InlineData("a b")]
    public void Format_rejects_wildcard_empty_or_whitespace_tokens(string value)
    {
        Assert.Throws<ArgumentException>(() => NatsSubject.Format("orders.{id}", ("id", value)));
    }
}
