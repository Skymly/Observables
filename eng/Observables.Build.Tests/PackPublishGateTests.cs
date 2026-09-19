namespace Observables.Build.Tests;

public sealed class PackPublishGateTests
{
    [Fact]
    public void Publish_requires_both_nuget_org_and_github_packages_credentials()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot.Find(), "build", "Program.cs"));

        Assert.Contains(
            ".Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey))",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Requires(() => !string.IsNullOrWhiteSpace(GitHubToken))",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey) || !string.IsNullOrWhiteSpace(GitHubToken))",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ci_shared_filter_includes_root_generator_props()
    {
        string yaml = File.ReadAllText(Path.Combine(RepoRoot.Find(), ".github", "workflows", "ci.yml"));

        Assert.Contains("Observables.SourceGenerators.props", yaml, StringComparison.Ordinal);
        Assert.Contains("Observables.SourceGenerators.R3.props", yaml, StringComparison.Ordinal);
        Assert.Contains("ci-full:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void PackCsprojReader_asks_runtime_packages_for_the_full_lib_tfm_matrix()
    {
        Assert.Equal(
            new[] { "netstandard2.0", "net8.0", "net9.0", "net10.0" },
            PackCsprojReader.DefaultLibTfms);
    }
}
