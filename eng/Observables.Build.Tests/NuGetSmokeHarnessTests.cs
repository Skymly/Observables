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
    public void Published_config_only_lists_nuget_org()
    {
        string configPath = Path.Combine(RepoRoot.Find(), "eng", "nuget-smoke", "nuget.config");
        XElement sources = Assert.Single(XDocument.Load(configPath).Descendants("packageSources"));

        Assert.NotNull(sources.Element("clear"));
        XElement source = Assert.Single(sources.Elements("add"));
        Assert.Equal(SmokeFeed.PublishedSource, (string?)source.Attribute("value"));
    }

    [Theory]
    [InlineData(SmokeFeed.PublishedSource, true)]
    [InlineData("https://example.invalid/v3/index.json", false)]
    public void Published_smoke_checks_the_restored_package_source(string source, bool official)
    {
        DirectoryInfo cache = Directory.CreateTempSubdirectory("observables-smoke-test-");
        try
        {
            string package = Directory.CreateDirectory(Path.Combine(cache.FullName, "observables.test.r3", "1.0.0")).FullName;
            File.WriteAllText(Path.Combine(package, "observables.test.r3.1.0.0.nupkg"), "package contents");
            File.WriteAllText(Path.Combine(package, ".nupkg.metadata"), $$"""{"source":"{{source}}"}""");

            if (official)
            {
                SmokeFeed.VerifyRestoredPackage(cache.FullName, "Observables.Test.R3", "1.0.0", localFeed: null);
            }
            else
            {
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                    SmokeFeed.VerifyRestoredPackage(cache.FullName, "Observables.Test.R3", "1.0.0", localFeed: null));
                Assert.Contains("was not restored from", error.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            cache.Delete(recursive: true);
        }
    }

    [Fact]
    public void Local_smoke_rejects_stale_bytes_at_the_same_version()
    {
        DirectoryInfo fixture = Directory.CreateTempSubdirectory("observables-smoke-test-");
        try
        {
            string cache = Path.Combine(fixture.FullName, "cache");
            string feed = Path.Combine(fixture.FullName, "feed");
            string restored = Directory.CreateDirectory(Path.Combine(cache, "observables.test.r3", "1.0.0")).FullName;
            Directory.CreateDirectory(feed);
            File.WriteAllText(Path.Combine(restored, "observables.test.r3.1.0.0.nupkg"), "original contents");
            string packed = Path.Combine(feed, "Observables.Test.R3.1.0.0.nupkg");
            File.WriteAllText(packed, "original contents");
            SmokeFeed.VerifyRestoredPackage(cache, "Observables.Test.R3", "1.0.0", feed);

            File.WriteAllText(packed, "new contents");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                SmokeFeed.VerifyRestoredPackage(cache, "Observables.Test.R3", "1.0.0", feed));

            Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            fixture.Delete(recursive: true);
        }
    }

    [Fact]
    public void Manifest_includes_strict_events_and_grpc_smoke_consumers()
    {
        string json = File.ReadAllText(Path.Combine(RepoRoot.Find(), "eng", "Observables.BuildManifest.json"));

        Assert.Contains("eng/nuget-smoke/Events.R3.RoutedEvents.Consumer/Events.R3.RoutedEvents.Consumer.csproj", json, StringComparison.Ordinal);
        Assert.Contains("eng/nuget-smoke/Events.Reactive.RoutedEvents.Consumer/Events.Reactive.RoutedEvents.Consumer.csproj", json, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("R3")]
    [InlineData("Reactive")]
    public void Events_routed_events_consumers_enable_WPF_and_raise_a_real_event(string backend)
    {
        string root = RepoRoot.Find();
        string consumer = $"Events.{backend}.RoutedEvents.Consumer";
        string csproj = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", consumer, consumer + ".csproj"));
        string program = File.ReadAllText(Path.Combine(root, "eng", "nuget-smoke", "Events.R3.RoutedEvents.Consumer", "Program.cs"));

        Assert.Contains("<ObservableRoutedEvents>true</ObservableRoutedEvents>", csproj, StringComparison.Ordinal);
        Assert.Contains("<UseWPF>true</UseWPF>", csproj, StringComparison.Ordinal);
        Assert.Contains("RoutedEvents(", program, StringComparison.Ordinal);
        Assert.Contains("RoutedEventHandlers(", program, StringComparison.Ordinal);
        Assert.Contains("RaiseEvent(", program, StringComparison.Ordinal);
    }
}
