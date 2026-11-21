namespace Observables.Postgres.Tests;

public sealed class PostgresChannelNameTests
{
    [Theory]
    [InlineData("wire_pg")]
    [InlineData("_Mixed9")]
    public void ValidateChannelName_accepts_identifiers(string name) =>
        PostgresProtocol.ValidateChannelName(name);

    [Theory]
    [InlineData("wire_pg\n")]
    [InlineData("wire_pg\r\n")]
    [InlineData("wire\npg")]
    public void ValidateChannelName_rejects_line_breaks(string name) =>
        Assert.Throws<ArgumentException>("channel", () => PostgresProtocol.ValidateChannelName(name));
}
