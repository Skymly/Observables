using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

    [Parameter("Max parallel test projects (0 = auto, 1 = sequential)")]
    readonly int TestParallelism = 0;

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

    int EffectiveTestParallelism =>
        TestParallelism switch
        {
            1 => 1,
            <= 0 => Math.Clamp(Environment.ProcessorCount, 2, 8),
            _ => TestParallelism,
        };

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
            Parallel.ForEach(
                Manifest.TestProjects,
                new ParallelOptions { MaxDegreeOfParallelism = EffectiveTestParallelism },
                relativePath =>
                {
                    string projectFile = ManifestPathGuard.RequireFile(relativePath, Root);
                    DotNetRestore(s => s.SetProjectFile(projectFile));
                });
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
                    || TestDomains.Contains("shared", StringComparer.OrdinalIgnoreCase)
                        && (p.StartsWith("Observables.Shared/", StringComparison.OrdinalIgnoreCase)
                            || p.StartsWith("eng/", StringComparison.OrdinalIgnoreCase)
                            || p.StartsWith("build/", StringComparison.OrdinalIgnoreCase)));

            var testProjects = projects
                .Select(relativePath =>
                {
                    ManifestPathGuard.RequireFile(relativePath, Root);
                    return Root / relativePath;
                })
                .ToArray();

            var parallelSafeProjects = testProjects
                .Where(projectFile => !IsE2ETestProject(projectFile))
                .ToArray();
            var e2eProjects = testProjects
                .Where(IsE2ETestProject)
                .ToArray();

            Parallel.ForEach(
                parallelSafeProjects,
                new ParallelOptions { MaxDegreeOfParallelism = EffectiveTestParallelism },
                RunTestProject);

            foreach (var projectFile in e2eProjects)
            {
                RunTestProject(projectFile);
            }

            AssertTrxCompleteness(testProjects);
        });

    static bool IsE2ETestProject(AbsolutePath projectFile)
    {
        string normalized = projectFile.ToString().Replace('\\', '/');
        if (normalized.Contains("/eng/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/build/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return projectFile.Name.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase)
            && !projectFile.Name.Contains("SourceGenerators", StringComparison.OrdinalIgnoreCase);
    }

    void RunTestProject(AbsolutePath projectFile)
    {
        foreach (string tfm in TestProjectTfms.GetFrameworks(projectFile))
        {
            DotNetTest(s => s
                .SetProjectFile(projectFile)
                .SetConfiguration(Configuration)
                .SetFramework(tfm)
                .EnableNoRestore()
                .SetResultsDirectory(TestResultsDirectory)
                .SetLoggers("trx;LogFileName=" + TestProjectTfms.TrxFileName(projectFile, tfm)));
        }
    }

    void AssertTrxCompleteness(IEnumerable<AbsolutePath> testProjects)
    {
        var missing = new List<string>();
        foreach (AbsolutePath projectFile in testProjects)
        {
            foreach (string tfm in TestProjectTfms.GetFrameworks(projectFile))
            {
                string fileName = TestProjectTfms.TrxFileName(projectFile, tfm);
                AbsolutePath trx = TestResultsDirectory / fileName;
                if (!trx.FileExists())
                {
                    missing.Add(fileName);
                    continue;
                }

                string contents = File.ReadAllText(trx);
                if (contents.IndexOf("<ResultSummary", StringComparison.Ordinal) < 0)
                {
                    missing.Add(fileName + " (no ResultSummary)");
                }
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Missing per-TFM TRX results (last-writer-wins would drop net8/net9 history): "
                + string.Join(", ", missing));
        }
    }

    Target Pack => _ => _
        .DependsOn(Restore)
        .Executes(() => PackPackages(FilteredPackages, Version));

    void PackPackages(IEnumerable<BuildManifest.PackageEntry> packages, string? versionOverride)
    {
        PackageOutputDirectory.CreateOrCleanDirectory();

        foreach (BuildManifest.PackageEntry package in packages)
        {
            string projectFile = ManifestPathGuard.RequireFile(package.PackProject, Root);

            DotNetPack(s =>
            {
                s = s
                    .SetProject(projectFile)
                    .SetConfiguration(Configuration)
                    .SetProperty("PackageOutputPath", PackageOutputDirectory)
                    .SetProperty("ContinuousIntegrationBuild", "true");

                if (!string.IsNullOrWhiteSpace(versionOverride))
                {
                    s = s
                        .SetVersion(versionOverride)
                        .SetProperty("PackageVersion", versionOverride);
                }

                return s;
            });
        }
    }

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

                string projectFile = ManifestPathGuard.RequireFile(package.PackProject, Root);
                NupkgVerifyRequest request = PackCsprojReader.FromPackProject(projectFile);
                if (!string.Equals(request.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{packageId}: pack csproj PackageId is '{request.PackageId}'");
                }

                IReadOnlyList<string> errors = NupkgVerifier.Verify(nupkg, request);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
                }
            }
        });

    Target NuGetConsumerSmoke => _ => _
        .DependsOn(ConsumerFeed == NuGetConsumerFeed.Local ? Restore : null)
        .DependsOn(ConsumerFeed == NuGetConsumerFeed.Local ? Test : null)
        .Executes(() =>
        {
            string packageVersion = ConsumerFeed == NuGetConsumerFeed.Local
                ? SmokeFeed.LocalPackageVersion(EffectivePackageVersion, ResolveShortGitSha())
                : EffectivePackageVersion;

            if (ConsumerFeed == NuGetConsumerFeed.Local)
            {
                PackPackages(Manifest.Packages, packageVersion);
            }

            foreach (string relativePath in Manifest.SmokeConsumers)
            {
                AbsolutePath projectFile = Root / relativePath;
                Assert.FileExists(projectFile, $"Consumer project not found: {projectFile}");

                if (ConsumerFeed == NuGetConsumerFeed.Local)
                {
                    DotNetRestore(s => s
                        .SetProjectFile(projectFile)
                        .SetConfigFile(NuGetSmokeLocalConfig)
                        .SetProperty("ObservablesConsumerPackageVersion", packageVersion)
                        .SetProperty("RestoreConfigFile", NuGetSmokeLocalConfig));
                }

                DotNetBuild(s =>
                {
                    s = s
                        .SetProjectFile(projectFile)
                        .SetConfiguration(Configuration)
                        .SetProperty("ObservablesConsumerPackageVersion", packageVersion);

                    if (ConsumerFeed == NuGetConsumerFeed.Local)
                    {
                        s = s
                            .EnableNoRestore()
                            .SetProperty("RestoreConfigFile", NuGetSmokeLocalConfig);
                    }

                    return s;
                });

                DotNetRun(s =>
                {
                    s = s
                        .SetProjectFile(projectFile)
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .SetProperty("ObservablesConsumerPackageVersion", packageVersion);

                    if (ConsumerFeed == NuGetConsumerFeed.Local)
                    {
                        s = s.SetProperty("RestoreConfigFile", NuGetSmokeLocalConfig);
                    }

                    return s;
                });
            }
        });

    string ResolveShortGitSha()
    {
        string? githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(githubSha))
        {
            return githubSha.Length <= 12 ? githubSha : githubSha[..12];
        }

        var startInfo = new ProcessStartInfo("git", "rev-parse --short=12 HEAD")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git to resolve the smoke package version.");
        string sha = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(sha))
        {
            throw new InvalidOperationException(
                "git rev-parse --short=12 HEAD failed: " + process.StandardError.ReadToEnd().Trim());
        }

        return sha;
    }

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

    Target FormatWhitespace => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            // Whitespace-only (EOL / trailing). Style/analyzer format is deferred:
            // RestAPI still uses block namespaces (IDE0161) from Refit-era sources.
            // Prefer running on LF checkouts (CI ubuntu); Windows CRLF working trees may fail.
            DotNet($"format whitespace {SolutionFile} --verify-no-changes --no-restore");
        });

    Target TrimPublish => _ => _
        .DependsOn(UnitTest)
        .Executes(() =>
        {
            AbsolutePath projectFile = Root / "Observables.Shared" / "Observables.TrimTests" / "Observables.TrimTests.csproj";
            string rid = OperatingSystem.IsWindows()
                ? "win-x64"
                : OperatingSystem.IsLinux()
                    ? "linux-x64"
                    : OperatingSystem.IsMacOS()
                        ? "osx-arm64"
                        : throw new PlatformNotSupportedException("Trim publish requires Windows, Linux, or macOS.");

            foreach (string tfm in new[] { "net8.0", "net10.0" })
            {
                DotNetPublish(s => s
                    .SetProject(projectFile)
                    .SetConfiguration(Configuration)
                    .SetFramework(tfm)
                    .SetRuntime(rid)
                    .SetSelfContained(true)
                    .EnableNoRestore()
                    .SetProperty("BuildProjectReferences", "false")
                    .SetProperty("PublishTrimmed", "true")
                    .SetProperty("TrimMode", "full")
                    .SetOutput(Root / "artifacts" / "trim" / tfm));
            }
        });

    Target Ci => _ => _
        .DependsOn(TrimPublish);

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
