using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    [Parameter("Build configuration (Debug/Release)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Package version override")]
    readonly string? Version = Environment.GetEnvironmentVariable("VERSION");

    [Parameter("NuGet consumer smoke feed: Local (artifacts/package) or Published (nuget.org)")]
    readonly NuGetConsumerFeed ConsumerFeed = NuGetConsumerFeed.Local;

    [Parameter("NuGet API key (required for nuget.org Publish)")]
    readonly string? NuGetApiKey =
        Environment.GetEnvironmentVariable("NUGET_API_KEY")
        ?? Environment.GetEnvironmentVariable("APIKEY");

    [Parameter("GitHub token with packages:write (required for GitHub Packages Publish)")]
    readonly string? GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    [Parameter("Comma-separated domain filter for Pack (e.g. 'restapi,signalr'). Empty = all domains.")]
    readonly string[] PackDomains = Array.Empty<string>();

    [Parameter("Comma-separated domain filter for UnitTest (e.g. 'mqtt,websocket'). Empty = all domains.")]
    readonly string[] TestDomains = Array.Empty<string>();

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "Observables.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";
    AbsolutePath NuGetSmokeDirectory => Root / "eng" / "nuget-smoke";
    AbsolutePath NuGetSmokeLocalConfig => NuGetSmokeDirectory / "nuget.config.local";
    AbsolutePath BuildManifestFile => Root / "eng" / "Observables.BuildManifest.json";
    AbsolutePath PackagePropsFile => Root / "eng" / "Observables.Package.props";

    BuildManifest Manifest => BuildManifest.Load(BuildManifestFile);

    string EffectivePackageVersion =>
        string.IsNullOrWhiteSpace(Version)
            ? PackageVersionReader.ReadFromProps(PackagePropsFile)
            : Version;

    bool DomainFilterActive => PackDomains.Length > 0;

    IEnumerable<BuildManifest.PackageEntry> FilteredPackages =>
        DomainFilterActive
            ? Manifest.Packages.Where(p => PackDomains.Any(d => p.PackageId.StartsWith($"Observables.{d}.", StringComparison.OrdinalIgnoreCase)))
            : Manifest.Packages;

    public static int Main() => Execute<Build>(x => x.Ci);

    Target Clean => _ => _
        .Executes(() =>
        {
            if (TestResultsDirectory.DirectoryExists())
            {
                TestResultsDirectory.DeleteDirectory();
            }

            TestResultsDirectory.CreateDirectory();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SolutionFile));

            // slnx /Tests/ nested folders are not in the solution restore graph.
            foreach (string relativePath in Manifest.TestProjects)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    continue;
                }

                DotNetRestore(s => s.SetProjectFile(projectFile));
            }
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            IEnumerable<string> projects = TestDomains.Length == 0
                ? Manifest.TestProjects
                : Manifest.TestProjects.Where(p =>
                    TestDomains.Any(d => p.StartsWith($"Observables.{d}/", StringComparison.OrdinalIgnoreCase))
                    || TestDomains.Contains("shared", StringComparer.OrdinalIgnoreCase) && p.StartsWith("Observables.Shared/", StringComparison.OrdinalIgnoreCase));

            foreach (string relativePath in projects)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    continue;
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore()
                    .SetProperty("BuildTfmsInParallel", "false")
                    .SetProperty("TestTfmsInParallel", "false")
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx"));
            }
        });

    Target Pack => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            PackageOutputDirectory.CreateOrCleanDirectory();

            foreach (BuildManifest.PackageEntry package in FilteredPackages)
            {
                AbsolutePath projectFile = Root / package.PackProject;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Pack project not found: {projectFile}");
                }

                DotNetPack(s =>
                {
                    s = s
                        .SetProject(projectFile)
                        .SetConfiguration(Configuration)
                        .SetProperty("PackageOutputPath", PackageOutputDirectory)
                        .SetProperty("ContinuousIntegrationBuild", "true");

                    if (!string.IsNullOrWhiteSpace(Version))
                    {
                        s = s.SetVersion(Version);
                    }

                    return s;
                });
            }
        });

    Target PackVerify => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            string packageVersion = EffectivePackageVersion;

            foreach (BuildManifest.PackageEntry package in FilteredPackages)
            {
                string packageId = package.PackageId;
                AbsolutePath nupkg = PackageOutputDirectory / $"{packageId}.{packageVersion}.nupkg";
                Assert.FileExists(nupkg, $"Expected package: {nupkg}");

                using ZipArchive archive = ZipFile.OpenRead(nupkg);
                HashSet<string> entries = archive.Entries
                    .Select(e => e.FullName.Replace('\\', '/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                bool hasAnalyzer = entries.Any(e => e.StartsWith("analyzers/dotnet/roslyn4.12/cs/", StringComparison.OrdinalIgnoreCase)
                    && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                Assert.True(hasAnalyzer, $"{packageId}: missing analyzer DLL under analyzers/dotnet/roslyn4.12/cs/");

                if (packageId.StartsWith("Observables.RestAPI.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.SignalR.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Mqtt.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.WebSocket.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Grpc.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Sse.", StringComparison.Ordinal))
                {
                    Assert.True(
                        entries.Contains("analyzers/dotnet/roslyn4.12/cs/Observables.CodeFixes.dll"),
                        $"{packageId}: missing Observables.CodeFixes.dll under analyzers/dotnet/roslyn4.12/cs/");
                    Assert.True(
                        entries.Contains("analyzers/dotnet/roslyn4.12/cs/Observables.Analyzers.dll"),
                        $"{packageId}: missing Observables.Analyzers.dll under analyzers/dotnet/roslyn4.12/cs/");
                }

                if (packageId.StartsWith("Observables.Events.", StringComparison.Ordinal))
                {
                    Assert.True(
                        entries.Contains("buildTransitive/observables.events.props"),
                        $"{packageId}: missing buildTransitive/observables.events.props");
                }

                if (packageId.StartsWith("Observables.RestAPI.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.SignalR.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Mqtt.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.WebSocket.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Grpc.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Sse.", StringComparison.Ordinal))
                {
                    bool hasLib = entries.Any(e => e.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                        && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                    Assert.True(hasLib, $"{packageId}: missing runtime assemblies under lib/");
                }

                Assert.True(
                    entries.Contains("README.md"),
                    $"{packageId}: missing package README.md at package root");
            }
        });

    Target NuGetConsumerSmoke => _ => _
        .DependsOn(ConsumerFeed == NuGetConsumerFeed.Local ? Pack : null)
        .DependsOn(ConsumerFeed == NuGetConsumerFeed.Local ? Test : null)
        .Executes(() =>
        {
            string packageVersion = EffectivePackageVersion;
            string? previousNuGetConfig = Environment.GetEnvironmentVariable("NUGET_CONFIG");

            if (ConsumerFeed == NuGetConsumerFeed.Local)
            {
                Environment.SetEnvironmentVariable("NUGET_CONFIG", NuGetSmokeLocalConfig);
            }

            try
            {
                foreach (string relativePath in Manifest.SmokeConsumers)
                {
                    AbsolutePath projectFile = Root / relativePath;
                    Assert.FileExists(projectFile, $"Consumer project not found: {projectFile}");

                    DotNetBuild(s => s
                        .SetProjectFile(projectFile)
                        .SetConfiguration(Configuration)
                        .SetProperty("ObservablesConsumerPackageVersion", packageVersion));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("NUGET_CONFIG", previousNuGetConfig);
            }
        });

    Target Publish => _ => _
        .DependsOn(Test, PackVerify)
        .Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey) || !string.IsNullOrWhiteSpace(GitHubToken))
        .Executes(() =>
        {
            AbsolutePath packages = PackageOutputDirectory / "*.nupkg";

            if (!string.IsNullOrWhiteSpace(NuGetApiKey))
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(packages)
                    .SetApiKey(NuGetApiKey)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .EnableSkipDuplicate());
            }

            if (!string.IsNullOrWhiteSpace(GitHubToken))
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(packages)
                    .SetApiKey(GitHubToken)
                    .SetSource("https://nuget.pkg.github.com/Skymly/index.json")
                    .EnableSkipDuplicate());
            }
        });

    Target Ci => _ => _
        .DependsOn(UnitTest);

    Target Test => _ => _
        .DependsOn(UnitTest);

    Target PackOnly => _ => _
        .DependsOn(Pack, PackVerify);

    Target CiPack => _ => _
        .DependsOn(Test, PackOnly)
        .DependsOn(NuGetConsumerSmoke)
        .OnlyWhenStatic(() => ConsumerFeed == NuGetConsumerFeed.Local);

    Target NuGetConsumerSmokePublished => _ => _
        .DependsOn(NuGetConsumerSmoke)
        .OnlyWhenStatic(() => ConsumerFeed == NuGetConsumerFeed.Published);
}

enum NuGetConsumerFeed
{
    Local,
    Published,
}
