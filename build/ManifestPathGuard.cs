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

        string full = Path.GetFullPath(
            Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(full))
        {
            throw new InvalidOperationException($"Build manifest path does not exist: {relativePath}");
        }

        return full;
    }
}
