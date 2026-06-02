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

    [Parameter("NuGet API key (required for nuget.org Publish)")]
    readonly string? NuGetApiKey =
        Environment.GetEnvironmentVariable("NUGET_API_KEY")
        ?? Environment.GetEnvironmentVariable("APIKEY");

    [Parameter("GitHub token with packages:write (required for GitHub Packages Publish)")]
    readonly string? GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "Observables.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";

    /// <summary>
    /// All test projects (slnx does not discover every test project via <c>dotnet test</c>).
    /// </summary>
    static readonly string[] TestProjectRelativePaths =
    [
        "Observables.Events/Observables.Events.R3.SourceGenerators.Tests/Observables.Events.R3.SourceGenerators.Tests.csproj",
        "Observables.Events/Observables.Events.Reactive.SourceGenerators.Tests/Observables.Events.Reactive.SourceGenerators.Tests.csproj",
        "Observables.RestAPI/Observables.RestAPI.Tests/Observables.RestAPI.Tests.csproj",
        "Observables.RestAPI/Observables.RestAPI.Reactive.Tests/Observables.RestAPI.Reactive.Tests.csproj",
        "Observables.RestAPI/Observables.RestAPI.GeneratorTests/Observables.RestAPI.GeneratorTests.csproj",
        "Observables.RestAPI/Observables.RestAPI.HttpClientFactory.Tests/Observables.RestAPI.HttpClientFactory.Tests.csproj",
    ];

    static readonly string[] PackProjectRelativePaths =
    [
        "Observables.Events/Observables.Events.Package/Observables.Events.R3.csproj",
        "Observables.Events/Observables.Events.Package/Observables.Events.Reactive.csproj",
        "Observables.RestAPI/Observables.RestAPI.Package/Observables.RestAPI.R3.csproj",
        "Observables.RestAPI/Observables.RestAPI.Package/Observables.RestAPI.Reactive.Pack.csproj",
    ];

    static readonly string[] ExpectedPackageIds =
    [
        "Observables.Events.R3",
        "Observables.Events.Reactive",
        "Observables.RestAPI.R3",
        "Observables.RestAPI.Reactive",
    ];

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
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    continue;
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx"));
            }
        });

    Target Pack => _ => _
        .DependsOn(UnitTest)
        .Executes(() =>
        {
            PackageOutputDirectory.CreateOrCleanDirectory();

            foreach (string relativePath in PackProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
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
            string versionSuffix = string.IsNullOrWhiteSpace(Version) ? "0.1.0-preview1" : Version;

            foreach (string packageId in ExpectedPackageIds)
            {
                AbsolutePath nupkg = PackageOutputDirectory / $"{packageId}.{versionSuffix}.nupkg";
                Assert.FileExists(nupkg, $"Expected package: {nupkg}");

                using ZipArchive archive = ZipFile.OpenRead(nupkg);
                HashSet<string> entries = archive.Entries
                    .Select(e => e.FullName.Replace('\\', '/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                bool hasAnalyzer = entries.Any(e => e.StartsWith("analyzers/dotnet/roslyn4.12/cs/", StringComparison.OrdinalIgnoreCase)
                    && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                Assert.True(hasAnalyzer, $"{packageId}: missing analyzer DLL under analyzers/dotnet/roslyn4.12/cs/");

                if (packageId.StartsWith("Observables.Events.", StringComparison.Ordinal))
                {
                    Assert.True(
                        entries.Contains("buildTransitive/observables.events.props"),
                        $"{packageId}: missing buildTransitive/observables.events.props");
                }

                if (packageId.StartsWith("Observables.RestAPI.", StringComparison.Ordinal))
                {
                    bool hasLib = entries.Any(e => e.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                        && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                    Assert.True(hasLib, $"{packageId}: missing runtime assemblies under lib/");
                }
            }
        });

    Target Publish => _ => _
        .DependsOn(PackVerify)
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

    Target CiPack => _ => _
        .DependsOn(PackVerify);
}
