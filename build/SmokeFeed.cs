static class SmokeFeed
{
    public static string LocalPackageVersion(string packageVersion, string gitSha)
    {
        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new ArgumentException("Package version is required.", nameof(packageVersion));
        }

        if (string.IsNullOrWhiteSpace(gitSha))
        {
            throw new ArgumentException("Git SHA is required for a local smoke version.", nameof(gitSha));
        }

        string sha = gitSha.Trim();
        if (sha.Length > 12)
        {
            sha = sha[..12];
        }

        return $"{packageVersion}-ci.{sha}";
    }
}
