namespace Observables.Build.Tests;

public sealed class ManifestPathGuardTests
{
    [Fact]
    public void RequireFile_throws_when_the_manifest_path_does_not_exist()
    {
        string root = Path.Combine(Path.GetTempPath(), $"obs-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => ManifestPathGuard.RequireFile("Observables.Missing/Observables.Missing.Tests.csproj", root));

            Assert.Contains(
                "Observables.Missing/Observables.Missing.Tests.csproj",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain("skip", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RequireFile_returns_the_full_path_when_the_file_exists()
    {
        string root = Path.Combine(Path.GetTempPath(), $"obs-manifest-{Guid.NewGuid():N}");
        string relative = Path.Combine("Observables.Demo", "Observables.Demo.Tests.csproj");
        string full = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "<Project />");

        try
        {
            string resolved = ManifestPathGuard.RequireFile(
                "Observables.Demo/Observables.Demo.Tests.csproj",
                root);

            Assert.True(File.Exists(resolved));
            Assert.Equal(Path.GetFullPath(full), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Restore_and_UnitTest_require_manifest_paths_instead_of_skipping()
    {
        string program = File.ReadAllText(Path.Combine(FindRepoRoot(), "build", "Program.cs"));

        Assert.Contains("ManifestPathGuard.RequireFile", program, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!projectFile.FileExists())", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Release_workflow_validates_PackageVersion_not_Version()
    {
        string yml = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "release.yml"));

        Assert.Contains("<PackageVersion>", yml, StringComparison.Ordinal);
        Assert.Contains("PackageVersion", yml, StringComparison.Ordinal);
        Assert.DoesNotContain("grep -oP '<Version>", yml, StringComparison.Ordinal);
    }

    static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Observables.slnx")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate Observables.slnx from " + AppContext.BaseDirectory);
    }
}
