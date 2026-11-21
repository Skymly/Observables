namespace Observables.Nats.Tests;

public sealed class NatsSubjectTests
{
    [Fact]
    public void Format_keeps_a_literal_token()
    {
        Assert.Equal("orders.42.cancel", NatsSubject.Format("orders.{id}.cancel", ("id", "42")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Format_inserts_braces_literally_in_either_parameter_order(bool reverseParameters)
    {
        (string Name, string? Value)[] parameters = [("tenant", "{region}"), ("region", "north")];
        if (reverseParameters)
        {
            Array.Reverse(parameters);
        }

        Assert.Equal(
            "orders.{region}.north.{region}.{unknown}",
            NatsSubject.Format("orders.{tenant}.{region}.{tenant}.{unknown}", parameters));
    }

    [Fact]
    public void Format_uses_the_first_value_for_a_repeated_binding() =>
        Assert.Equal("orders.first.first", NatsSubject.Format("orders.{id}.{id}", ("id", "first"), ("id", "second")));

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
