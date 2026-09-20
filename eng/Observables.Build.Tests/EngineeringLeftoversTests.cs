namespace Observables.Build.Tests;

public sealed class EngineeringLeftoversTests
{
    [Fact]
    public void PublicAPI_trees_include_net10_and_r3_bridges()
    {
        string root = RepoRoot.Find();
        string[] dirs = Directory.GetDirectories(root, "PublicAPI", SearchOption.AllDirectories);
        Assert.Contains(dirs, d => d.Contains("Observables.Redis", StringComparison.Ordinal));
        Assert.Contains(dirs, d => d.Contains("Observables.Postgres", StringComparison.Ordinal));
        Assert.Contains(dirs, d => d.Contains("Observables.Mqtt.R3", StringComparison.Ordinal));
        Assert.Contains(
            dirs,
            d => Directory.Exists(Path.Combine(d, "net10.0")));
    }

    [Fact]
    public void Bootstrap_public_api_script_discovers_projects_instead_of_a_frozen_m7_list()
    {
        string script = File.ReadAllText(
            Path.Combine(RepoRoot.Find(), "eng", "scripts", "bootstrap-public-api.ps1"));
        Assert.Contains("Get-PublicApiProjects", script, StringComparison.Ordinal);
        Assert.Contains("Get-PublicApiTfms", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'Observables.Nats/Observables.Nats/Observables.Nats.csproj'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RestAPI_packages_pack_NOTICE()
    {
        string props = File.ReadAllText(Path.Combine(RepoRoot.Find(), "eng", "Observables.Package.props"));
        Assert.Contains("NOTICE.md", props, StringComparison.Ordinal);
        Assert.Contains("Observables.RestAPI.", props, StringComparison.Ordinal);
    }

    [Fact]
    public void Agents_md_matches_public_repo_and_nupkg_events_props_names()
    {
        string agents = File.ReadAllText(Path.Combine(RepoRoot.Find(), "AGENTS.md"));
        Assert.DoesNotContain("Observables（私有）", agents, StringComparison.Ordinal);
        Assert.Contains("Observables（公开）", agents, StringComparison.Ordinal);
        Assert.Contains("Observables.Events.R3.props", agents, StringComparison.Ordinal);
        Assert.Contains("十个域均已有 Package", agents, StringComparison.Ordinal);
    }

    [Fact]
    public void Ci_pins_actionlint_tarball_checksum()
    {
        string yaml = File.ReadAllText(Path.Combine(RepoRoot.Find(), ".github", "workflows", "ci.yml"));
        Assert.Contains("sha256sum --check --strict", yaml, StringComparison.Ordinal);
        Assert.Contains("8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8", yaml, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@11d5960a326750d5838078e36cf38b85af677262", yaml, StringComparison.Ordinal);
    }
}
