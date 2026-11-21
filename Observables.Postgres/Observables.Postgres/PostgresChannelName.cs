using System.Text.RegularExpressions;

namespace Observables.Postgres;

internal static class PostgresChannelName
{
    static readonly Regex ChannelNameRegex = new(
        @"\A[A-Za-z_][A-Za-z0-9_]*\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool IsValid(string channel) =>
        channel.Length is > 0 and <= 63 && ChannelNameRegex.IsMatch(channel);
}
