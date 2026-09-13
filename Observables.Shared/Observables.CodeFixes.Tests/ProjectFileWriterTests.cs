using Microsoft.CodeAnalysis;

namespace Observables.CodeFixes.Tests;

public sealed class ProjectFileWriterTests
{
    const string OriginalCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Observables.RestAPI.R3" Version="0.2.2" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public async Task AddPackageReferenceAsync_does_not_write_the_csproj_on_disk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obs-codefix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csprojPath = Path.Combine(dir, "Consumer.csproj");
        File.WriteAllText(csprojPath, OriginalCsproj);

        try
        {
            using var workspace = new AdhocWorkspace();
            var project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "Consumer",
                "Consumer",
                LanguageNames.CSharp,
                filePath: csprojPath));

            var solution = await AddRuntimePackageReferenceCodeFixProvider.AddPackageReferenceAsync(
                project,
                "Observables.RestAPI",
                version: "0.2.2",
                TestContext.Current.CancellationToken);

            Assert.Equal(OriginalCsproj, File.ReadAllText(csprojPath));

            var documentId = Assert.Single(solution.GetDocumentIdsWithFilePath(csprojPath));
            var additional = solution.GetAdditionalDocument(documentId);
            Assert.NotNull(additional);
            var text = await additional.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.Contains(
                "<PackageReference Include=\"Observables.RestAPI\" Version=\"0.2.2\" />",
                text.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
