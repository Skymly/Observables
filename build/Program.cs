using System;
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

    [Parameter("NuGet API key (required for Publish target)")]
    readonly string? NuGetApiKey =
        Environment.GetEnvironmentVariable("NUGET_API_KEY")
        ?? Environment.GetEnvironmentVariable("APIKEY");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "Observables.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";

    /// <summary>
    /// All test projects (slnx does not discover every test project via <c>dotnet test</c>).
    /// </summary>
    static readonly string[] TestProjectRelativePaths =
    [
        "Observables.Events.R3.SourceGenerators.Tests/Observables.Events.R3.SourceGenerators.Tests.csproj",
        "Observables.Events.Reactive.SourceGenerators.Tests/Observables.Events.Reactive.SourceGenerators.Tests.csproj",
        "Observables.RestAPI.Tests/Observables.RestAPI.Tests.csproj",
        "Observables.RestAPI.Reactive.Tests/Observables.RestAPI.Reactive.Tests.csproj",
        "Observables.RestAPI.GeneratorTests/Observables.RestAPI.GeneratorTests.csproj",
        "Observables.RestAPI.HttpClientFactory.Tests/Observables.RestAPI.HttpClientFactory.Tests.csproj",
    ];

    static readonly string[] PackProjectRelativePaths =
    [
        "Observables.Events.Package/Observables.Events.Package.csproj",
        "Observables.RestAPI.Package/Observables.RestAPI.Package.csproj",
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
        .DependsOn(Compile)
        .Executes(() =>
        {
            PackageOutputDirectory.CreateOrCleanDirectory();

            foreach (string relativePath in PackProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    continue;
                }

                DotNetPack(s =>
                {
                    s = s
                        .SetProject(projectFile)
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
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

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey))
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
                .SetTargetPath(PackageOutputDirectory / "*.nupkg")
                .SetApiKey(NuGetApiKey)
                .SetSource("https://api.nuget.org/v3/index.json")
                .EnableSkipDuplicate());
        });

    Target Ci => _ => _
        .DependsOn(UnitTest);
}
