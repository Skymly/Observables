using System.Text.Json;

using Nuke.Common;
using Nuke.Common.IO;

sealed class BuildManifest
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public required PackageEntry[] Packages { get; init; }

    public required string[] TestProjects { get; init; }

    public required string[] SmokeConsumers { get; init; }

    public static BuildManifest Load(AbsolutePath manifestFile)
    {
        Assert.FileExists(manifestFile, $"Build manifest not found: {manifestFile}");

        string json = File.ReadAllText(manifestFile);
        BuildManifest? manifest = JsonSerializer.Deserialize<BuildManifest>(json, JsonOptions);

        if (manifest is null
            || manifest.Packages.Length == 0
            || manifest.TestProjects.Length == 0
            || manifest.SmokeConsumers.Length == 0)
        {
            throw new InvalidOperationException($"Build manifest is empty or invalid: {manifestFile}");
        }

        return manifest;
    }

    public sealed class PackageEntry
    {
        public required string PackProject { get; init; }

        public required string PackageId { get; init; }
    }
}
