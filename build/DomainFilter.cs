static class DomainFilter
{
    public static T[] Select<T>(
        T[] entries, string[] arguments, string option, Func<T, string, bool> matchesDomain)
    {
        if (arguments.Length == 0)
        {
            return entries;
        }

        string[] domains = arguments
            .SelectMany(argument => argument.Split(',', StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string domain in domains)
        {
            if (domain.Length == 0 || !entries.Any(entry => matchesDomain(entry, domain)))
            {
                throw new ArgumentException($"{option}: no entries match domain '{domain}'.");
            }
        }

        return entries.Where(entry => domains.Any(domain => matchesDomain(entry, domain))).ToArray();
    }
}
