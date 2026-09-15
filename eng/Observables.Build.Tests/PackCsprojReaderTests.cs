namespace Observables.Build.Tests;

public sealed class PackCsprojReaderTests
{
    [Fact]
    public void FromPackProject_requires_lib_package_references_except_reactive_backends()
    {
        string root = CreateTree(
            packCsproj: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <PackageId>Observables.Grpc.R3</PackageId>
                    <ObservablesPackGeneratorAssemblyName>Observables.Grpc.R3.SourceGenerators.dll</ObservablesPackGeneratorAssemblyName>
                  </PropertyGroup>
                  <ItemGroup>
                    <ObservablesPackLibProjectReference Include="..\Observables.Grpc\Observables.Grpc.csproj" />
                    <PackageReference Include="R3" />
                    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """,
            libCsproj: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="Grpc.Core.Api" />
                    <PackageReference Include="Google.Protobuf" />
                    <PackageReference Include="R3" />
                  </ItemGroup>
                </Project>
                """);

        try
        {
            NupkgVerifyRequest request = PackCsprojReader.FromPackProject(
                Path.Combine(root, "Observables.Grpc.Package", "Observables.Grpc.R3.csproj"));

            Assert.Equal("Observables.Grpc.R3", request.PackageId);
            Assert.Equal("Observables.Grpc.R3.SourceGenerators.dll", request.GeneratorAssemblyFileName);
            Assert.Equal(
                ["netstandard2.0", "net8.0", "net9.0", "net10.0"],
                request.RequiredLibTfms);
            Assert.Contains("Grpc.Core.Api", request.RequiredDependenciesByTfm["netstandard2.0"]);
            Assert.Contains("Google.Protobuf", request.RequiredDependenciesByTfm["netstandard2.0"]);
            Assert.Contains("R3", request.RequiredDependenciesByTfm["netstandard2.0"]);
            Assert.DoesNotContain("Microsoft.SourceLink.GitHub", request.RequiredDependenciesByTfm["netstandard2.0"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FromPackProject_applies_tfm_conditioned_package_references_only_to_that_tfm()
    {
        string root = CreateTree(
            packCsproj: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <PackageId>Observables.RestAPI.R3</PackageId>
                    <ObservablesPackGeneratorAssemblyName>Observables.RestAPI.R3.SourceGenerators.dll</ObservablesPackGeneratorAssemblyName>
                  </PropertyGroup>
                  <ItemGroup>
                    <ObservablesPackLibProjectReference Include="..\Observables.RestAPI\Observables.RestAPI.csproj" />
                    <PackageReference Include="R3" />
                  </ItemGroup>
                </Project>
                """,
            libCsproj: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <ItemGroup>
                    <PackageReference Include="R3" />
                  </ItemGroup>
                  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
                    <PackageReference Include="System.Text.Json" />
                    <PackageReference Include="System.Net.Http.Json" />
                  </ItemGroup>
                </Project>
                """,
            libFolderName: "Observables.RestAPI");

        try
        {
            NupkgVerifyRequest request = PackCsprojReader.FromPackProject(
                Path.Combine(root, "Observables.RestAPI.Package", "Observables.RestAPI.R3.csproj"));

            Assert.Contains("System.Text.Json", request.RequiredDependenciesByTfm["netstandard2.0"]);
            Assert.Contains("System.Net.Http.Json", request.RequiredDependenciesByTfm["netstandard2.0"]);
            Assert.DoesNotContain("System.Text.Json", request.RequiredDependenciesByTfm["net8.0"]);
            Assert.DoesNotContain("System.Net.Http.Json", request.RequiredDependenciesByTfm["net8.0"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    static string CreateTree(string packCsproj, string libCsproj, string libFolderName = "Observables.Grpc")
    {
        string root = Path.Combine(Path.GetTempPath(), $"obs-packcsproj-{Guid.NewGuid():N}");
        string packDir = Path.Combine(root, $"{libFolderName}.Package");
        string libDir = Path.Combine(root, libFolderName);
        Directory.CreateDirectory(packDir);
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(packDir, $"{libFolderName}.R3.csproj"), packCsproj);
        File.WriteAllText(Path.Combine(libDir, $"{libFolderName}.csproj"), libCsproj);
        return root;
    }

    [Theory]
    [InlineData("Observables.Events.Reactive", "R3")]
    [InlineData("Observables.Events.R3", "System.Reactive")]
    [InlineData("Observables.RestAPI.Reactive", "R3")]
    public void ForbiddenBackendAssemblies_names_the_backend_a_split_domain_must_not_ship(
        string packageId,
        string expected)
    {
        Assert.Equal([expected], PackCsprojReader.ForbiddenBackendAssemblies(packageId));
    }

    [Fact]
    public void ForbiddenBackendAssemblies_is_empty_while_a_domain_awaits_its_split()
    {
        foreach (string domain in PackCsprojReader.DomainsPendingBackendSplit)
        {
            Assert.Empty(PackCsprojReader.ForbiddenBackendAssemblies($"Observables.{domain}.R3"));
            Assert.Empty(PackCsprojReader.ForbiddenBackendAssemblies($"Observables.{domain}.Reactive"));
        }
    }

    [Fact]
    public void ResolveIncludePath_finds_a_file_when_the_include_uses_windows_separators()
    {
        string root = Path.Combine(Path.GetTempPath(), $"obs-include-{Guid.NewGuid():N}");
        string packDir = Path.Combine(root, "Package");
        string libDir = Path.Combine(root, "Lib");
        Directory.CreateDirectory(packDir);
        Directory.CreateDirectory(libDir);
        string lib = Path.Combine(libDir, "Lib.csproj");
        File.WriteAllText(lib, "<Project />");

        try
        {
            string resolved = PackCsprojReader.ResolveIncludePath(packDir, @"..\Lib\Lib.csproj");
            Assert.True(File.Exists(resolved), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
