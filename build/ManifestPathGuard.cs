static class ManifestPathGuard
{
    public static string RequireFile(string relativePath, string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Manifest path is required.", nameof(relativePath));
        }

        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("Repository root is required.", nameof(repoRoot));
        }

        string rootFull = Path.GetFullPath(repoRoot);
        string full = Path.GetFullPath(
            Path.Combine(rootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        // Relative manifest paths inherit the root's spelling. A differently cased path
        // cannot prove containment on a case-sensitive directory.
        if (!full.StartsWith(prefix, StringComparison.Ordinal)
            && !string.Equals(full, rootFull, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Build manifest path escapes the repository root: {relativePath}");
        }

        if (!File.Exists(full))
        {
            throw new InvalidOperationException($"Build manifest path does not exist: {relativePath}");
        }

        return full;
    }
}
