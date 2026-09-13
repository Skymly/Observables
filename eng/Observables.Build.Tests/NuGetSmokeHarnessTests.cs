using System.Xml.Linq;

namespace Observables.Build.Tests;

public sealed class NuGetSmokeHarnessTests
{
    [Fact]
    public void Nuget_config_local_maps_Observables_packages_only_to_the_local_source()
    {
        string configPath = Path.Combine(RepoRoot.Find(), "eng", "nuget-smoke", "nuget.config.local");
        XDocument doc = XDocument.Load(configPath);

        XElement? mapping = doc.Descendants("packageSourceMapping").SingleOrDefault();
        Assert.NotNull(mapping);

        XElement local = mapping.Elements("packageSource")
            .Single(static e => (string?)e.Attribute("key") == "local");
        Assert.Contains(
            local.Elements("package").Select(static e => (string?)e.Attribute("pattern")),
            static pattern => pattern == "Observables.*");
        Assert.DoesNotContain(
            local.Elements("package").Select(static e => (string?)e.Attribute("pattern")),
            static pattern => pattern == "*");

        XElement nugetOrg = mapping.Elements("packageSource")
            .Single(static e => (string?)e.Attribute("key") == "nuget.org");
        Assert.Contains(
            nugetOrg.Elements("package").Select(static e => (string?)e.Attribute("pattern")),
            static pattern => pattern == "*");
    }

    [Fact]
    public void Local_smoke_package_version_uses_ci_sha_suffix_and_is_not_the_released_id()
    {
        string version = SmokeFeed.LocalPackageVersion("0.2.2", "10370d2");

        Assert.Equal("0.2.2-ci.10370d2", version);
        Assert.NotEqual("0.2.2", version);
    }

    [Fact]
    public void Nuke_smoke_target_passes_RestoreConfigFile_and_does_not_set_NUGET_CONFIG()
    {
        string program = File.ReadAllText(Path.Combine(RepoRoot.Find(), "build", "Program.cs"));

        Assert.DoesNotContain("SetEnvironmentVariable(\"NUGET_CONFIG\"", program, StringComparison.Ordinal);
        Assert.Contains("RestoreConfigFile", program, StringComparison.Ordinal);
        Assert.Contains("SmokeFeed.LocalPackageVersion", program, StringComparison.Ordinal);
        Assert.Contains("SetProperty(\"PackageVersion\"", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_includes_strict_events_and_grpc_smoke_consumers()
    {
        string json = File.ReadAllText(Path.Combine(RepoRoot.Find(), "eng", "Observables.BuildManifest.json"));

        Assert.Contains("eng/nuget-smoke/Events.R3.RoutedEvents.Consumer/Events.R3.RoutedEvents.Consumer.csproj", json, StringComparison.Ordinal);
        Assert.Contains("eng/nuget-smoke/Grpc.R3.NoTransport.Consumer/Grpc.R3.NoTransport.Consumer.csproj", json, StringComparison.Ordinal);
        Assert.Contains("eng/nuget-smoke/Grpc.Reactive.NoTransport.Consumer/Grpc.Reactive.NoTransport.Consumer.csproj", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Grpc_no_transport_consumers_do_not_reference_grpc_client_packages()
    {
        string root = RepoRoot.Find();
        string r3 = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", "Grpc.R3.NoTransport.Consumer", "Grpc.R3.NoTransport.Consumer.csproj"));
        string reactive = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", "Grpc.Reactive.NoTransport.Consumer", "Grpc.Reactive.NoTransport.Consumer.csproj"));

        Assert.DoesNotContain("Grpc.Net.Client", r3, StringComparison.Ordinal);
        Assert.DoesNotContain("Grpc.Core.Api", r3, StringComparison.Ordinal);
        Assert.DoesNotContain("Grpc.Net.Client", reactive, StringComparison.Ordinal);
        Assert.DoesNotContain("Grpc.Core.Api", reactive, StringComparison.Ordinal);
        Assert.Contains("Observables.Grpc.R3", r3, StringComparison.Ordinal);
        Assert.Contains("Observables.Grpc.Reactive", reactive, StringComparison.Ordinal);
    }

    [Fact]
    public void Events_routed_events_consumer_requires_the_msbuild_switch_and_routed_api()
    {
        string root = RepoRoot.Find();
        string csproj = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", "Events.R3.RoutedEvents.Consumer", "Events.R3.RoutedEvents.Consumer.csproj"));
        string program = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", "Events.R3.RoutedEvents.Consumer", "Program.cs"));

        Assert.Contains("<ObservableRoutedEvents>true</ObservableRoutedEvents>", csproj, StringComparison.Ordinal);
        Assert.Contains("<UseWPF>true</UseWPF>", csproj, StringComparison.Ordinal);
        Assert.Contains("RoutedEvents()", program, StringComparison.Ordinal);
    }
}
