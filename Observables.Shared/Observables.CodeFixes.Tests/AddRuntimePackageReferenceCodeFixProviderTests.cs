using Microsoft.CodeAnalysis.CodeActions;

namespace Observables.CodeFixes.Tests;

public sealed class AddRuntimePackageReferenceCodeFixProviderTests
{
    [Fact]
    public async Task RegisterCodeFixes_offers_only_published_backend_packages()
    {
        var directory = Directory.CreateTempSubdirectory("obs-codefix-");
        try
        {
            var path = Path.Combine(directory.FullName, "Consumer.csproj");
            File.WriteAllText(path, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Observables.Redis.R3" Version="0.4.0" />
                  </ItemGroup>
                </Project>
                """);
            using var workspace = new ProjectFileWriterTests.ProjectTestWorkspace(path);
            var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
            var actions = await ProjectFileWriterTests.GetActionsAsync(
                new AddRuntimePackageReferenceCodeFixProvider(), document, "OBS4002");

            Assert.Equal(
                [
                    "Add package reference to 'Observables.SignalR.R3'",
                    "Add package reference to 'Observables.SignalR.Reactive'",
                ],
                actions.Select(action => action.Title));

            var changes = await actions[0].GetOperationsAsync(TestContext.Current.CancellationToken);
            var operation = Assert.IsType<ApplyChangesOperation>(Assert.Single(changes));
            Assert.True(workspace.TryApplyChanges(operation.ChangedSolution));

            Assert.False(string.IsNullOrWhiteSpace(PackageReferenceVersion.DefaultVersion));
            Assert.Contains($"Include=\"Observables.SignalR.R3\" Version=\"{PackageReferenceVersion.DefaultVersion}\"",
                File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Sibling_version_uses_the_same_domain_and_reads_child_metadata()
    {
        const string content = """
            <Project>
              <ItemGroup>
                <PackageReference Include="observables.mqtt.reactive">
                  <Version>0.4.0</Version>
                </PackageReference>
                <PackageReference Include="Observables.Redis.R3" Version="0.4.1" />
              </ItemGroup>
            </Project>
            """;
        Assert.Equal("0.4.0", PackageReferenceVersion.InferSiblingVersion(content, "Observables.Mqtt.R3"));
        Assert.Null(PackageReferenceVersion.InferSiblingVersion(content, "Observables.SignalR.R3"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Central_versions_are_used_only_when_a_matching_version_exists(
        bool centralHasVersion, bool projectOptsOut)
    {
        var directory = Directory.CreateTempSubdirectory("obs-codefix-");
        try
        {
            var reference = centralHasVersion
                ? "<PackageVersion Include=\"Observables.Mqtt.Reactive\" Version=\"0.4.0\" />" : "";
            File.WriteAllText(Path.Combine(directory.FullName, "Directory.Packages.props"), $$"""
                <Project>
                  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
                  <ItemGroup>{{reference}}</ItemGroup>
                </Project>
                """);
            var projectProperty = projectOptsOut
                ? "<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>" : "";
            var path = Path.Combine(directory.FullName, "Consumer.csproj");
            File.WriteAllText(path, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>{{projectProperty}}</PropertyGroup>
                </Project>
                """);
            using var workspace = new ProjectFileWriterTests.ProjectTestWorkspace(path);
            var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
            var actions = await ProjectFileWriterTests.GetActionsAsync(
                new SwitchToReactivePackageCodeFixProvider(), document, "OBS5005");

            if (!centralHasVersion && !projectOptsOut)
            {
                Assert.Empty(actions);
                return;
            }

            var action = Assert.Single(actions);
            var operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);
            var operation = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations));
            Assert.True(workspace.TryApplyChanges(operation.ChangedSolution));
            var content = File.ReadAllText(path);
            Assert.True(CsprojPackageReferenceEditor.ContainsPackageReference(content, "Observables.Mqtt.Reactive"));
            Assert.Equal(projectOptsOut ? PackageReferenceVersion.DefaultVersion : null,
                CsprojPackageReferenceEditor.TryGetPackageVersion(content, "Observables.Mqtt.Reactive"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
