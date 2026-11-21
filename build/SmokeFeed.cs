using System.Security.Cryptography;
using System.Text.Json;

static class SmokeFeed
{
    public const string PublishedSource = "https://api.nuget.org/v3/index.json";

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

    public static void VerifyRestoredPackage(string cache, string packageId, string version, string? localFeed)
    {
        string id = packageId.ToLowerInvariant();
        string normalizedVersion = version.ToLowerInvariant();
        string packageDirectory = Path.Combine(cache, id, normalizedVersion);
        string restoredPackage = Path.Combine(packageDirectory, $"{id}.{normalizedVersion}.nupkg");
        if (!File.Exists(restoredPackage))
        {
            throw new InvalidOperationException($"{packageId} {version} was not restored into the smoke package cache.");
        }

        if (localFeed is null)
        {
            using FileStream metadata = File.OpenRead(Path.Combine(packageDirectory, ".nupkg.metadata"));
            using JsonDocument document = JsonDocument.Parse(metadata);
            if (!document.RootElement.TryGetProperty("source", out JsonElement source)
                || !string.Equals(source.GetString(), PublishedSource, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{packageId} {version} was not restored from {PublishedSource}.");
            }

            return;
        }

        using FileStream packed = File.OpenRead(Path.Combine(localFeed, $"{packageId}.{version}.nupkg"));
        using FileStream restored = File.OpenRead(restoredPackage);
        if (!SHA256.HashData(packed).AsSpan().SequenceEqual(SHA256.HashData(restored)))
        {
            throw new InvalidOperationException($"{packageId} {version} does not match the package built for this smoke run.");
        }
    }
}
