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

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "Observables.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";
    AbsolutePath NuGetSmokeDirectory => Root / "eng" / "nuget-smoke";
    AbsolutePath NuGetSmokeLocalConfig => NuGetSmokeDirectory / "nuget.config.local";

    static readonly string[] NuGetConsumerProjectRelativePaths =
    [
        "eng/nuget-smoke/Events.R3.Consumer/Events.R3.Consumer.csproj",
        "eng/nuget-smoke/Events.Reactive.Consumer/Events.Reactive.Consumer.csproj",
        "eng/nuget-smoke/RestAPI.R3.Consumer/RestAPI.R3.Consumer.csproj",
        "eng/nuget-smoke/RestAPI.Reactive.Consumer/RestAPI.Reactive.Consumer.csproj",
        "eng/nuget-smoke/SignalR.R3.Consumer/SignalR.R3.Consumer.csproj",
        "eng/nuget-smoke/SignalR.Reactive.Consumer/SignalR.Reactive.Consumer.csproj",
        "eng/nuget-smoke/Mqtt.R3.Consumer/Mqtt.R3.Consumer.csproj",
        "eng/nuget-smoke/Mqtt.Reactive.Consumer/Mqtt.Reactive.Consumer.csproj",
        "eng/nuget-smoke/WebSocket.R3.Consumer/WebSocket.R3.Consumer.csproj",
        "eng/nuget-smoke/WebSocket.Reactive.Consumer/WebSocket.Reactive.Consumer.csproj",
    ];

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
        "Observables.SignalR/Observables.SignalR.R3.SourceGenerators.Tests/Observables.SignalR.R3.SourceGenerators.Tests.csproj",
        "Observables.SignalR/Observables.SignalR.Reactive.SourceGenerators.Tests/Observables.SignalR.Reactive.SourceGenerators.Tests.csproj",
        "Observables.SignalR/Observables.SignalR.Tests/Observables.SignalR.Tests.csproj",
        "Observables.SignalR/Observables.SignalR.Reactive.Tests/Observables.SignalR.Reactive.Tests.csproj",
        "Observables.Mqtt/Observables.Mqtt.R3.SourceGenerators.Tests/Observables.Mqtt.R3.SourceGenerators.Tests.csproj",
        "Observables.Mqtt/Observables.Mqtt.Reactive.SourceGenerators.Tests/Observables.Mqtt.Reactive.SourceGenerators.Tests.csproj",
        "Observables.Mqtt/Observables.Mqtt.Tests/Observables.Mqtt.Tests.csproj",
        "Observables.Mqtt/Observables.Mqtt.Reactive.Tests/Observables.Mqtt.Reactive.Tests.csproj",
        "Observables.WebSocket/Observables.WebSocket.R3.SourceGenerators.Tests/Observables.WebSocket.R3.SourceGenerators.Tests.csproj",
        "Observables.WebSocket/Observables.WebSocket.Reactive.SourceGenerators.Tests/Observables.WebSocket.Reactive.SourceGenerators.Tests.csproj",
        "Observables.WebSocket/Observables.WebSocket.Tests/Observables.WebSocket.Tests.csproj",
        "Observables.WebSocket/Observables.WebSocket.Reactive.Tests/Observables.WebSocket.Reactive.Tests.csproj",
    ];

    static readonly string[] PackProjectRelativePaths =
    [
        "Observables.Events/Observables.Events.Package/Observables.Events.R3.csproj",
        "Observables.Events/Observables.Events.Package/Observables.Events.Reactive.csproj",
        "Observables.RestAPI/Observables.RestAPI.Package/Observables.RestAPI.R3.csproj",
        "Observables.RestAPI/Observables.RestAPI.Package/Observables.RestAPI.Reactive.Pack.csproj",
        "Observables.SignalR/Observables.SignalR.Package/Observables.SignalR.R3.csproj",
        "Observables.SignalR/Observables.SignalR.Package/Observables.SignalR.Reactive.Pack.csproj",
        "Observables.Mqtt/Observables.Mqtt.Package/Observables.Mqtt.R3.csproj",
        "Observables.Mqtt/Observables.Mqtt.Package/Observables.Mqtt.Reactive.Pack.csproj",
        "Observables.WebSocket/Observables.WebSocket.Package/Observables.WebSocket.R3.csproj",
        "Observables.WebSocket/Observables.WebSocket.Package/Observables.WebSocket.Reactive.Pack.csproj",
    ];

    static readonly string[] ExpectedPackageIds =
    [
        "Observables.Events.R3",
        "Observables.Events.Reactive",
        "Observables.RestAPI.R3",
        "Observables.RestAPI.Reactive",
        "Observables.SignalR.R3",
        "Observables.SignalR.Reactive",
        "Observables.Mqtt.R3",
        "Observables.Mqtt.Reactive",
        "Observables.WebSocket.R3",
        "Observables.WebSocket.Reactive",
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
            string versionSuffix = string.IsNullOrWhiteSpace(Version) ? "0.1.0-preview4" : Version;

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

                if (packageId.StartsWith("Observables.RestAPI.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.SignalR.", StringComparison.Ordinal)
                    || packageId.StartsWith("Observables.Mqtt.", StringComparison.Ordinal))
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
        .Executes(() =>
        {
            string packageVersion = string.IsNullOrWhiteSpace(Version) ? "0.1.0-preview4" : Version;
            string? previousNuGetConfig = Environment.GetEnvironmentVariable("NUGET_CONFIG");

            if (ConsumerFeed == NuGetConsumerFeed.Local)
            {
                Environment.SetEnvironmentVariable("NUGET_CONFIG", NuGetSmokeLocalConfig);
            }

            try
            {
                foreach (string relativePath in NuGetConsumerProjectRelativePaths)
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
        .DependsOn(PackVerify)
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

