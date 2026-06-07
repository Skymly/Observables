namespace Observables.CodeFixes.Tests;

public sealed class CsprojPackageReferenceEditorTests
{
    const string SampleCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Observables.RestAPI.R3" Version="0.1.0-preview4" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void AddPackageReferenceIfMissing_appends_to_existing_item_group()
    {
        var updated = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            SampleCsproj,
            "Observables.RestAPI",
            version: "0.1.0-preview4");

        Assert.Contains("<PackageReference Include=\"Observables.RestAPI\" Version=\"0.1.0-preview4\" />", updated);
        Assert.Equal(2, updated.Split("<PackageReference", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void AddPackageReferenceIfMissing_is_idempotent()
    {
        var once = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            SampleCsproj,
            "Observables.RestAPI.Reactive",
            version: null);
        var twice = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            once,
            "Observables.RestAPI.Reactive",
            version: null);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ReplacePackageReference_swaps_r3_for_reactive()
    {
        var updated = CsprojPackageReferenceEditor.ReplacePackageReference(
            SampleCsproj,
            "Observables.RestAPI.R3",
            "Observables.RestAPI.Reactive",
            version: "0.1.0-preview4");

        Assert.DoesNotContain("Observables.RestAPI.R3", updated);
        Assert.Contains("<PackageReference Include=\"Observables.RestAPI.Reactive\" Version=\"0.1.0-preview4\" />", updated);
    }

    [Fact]
    public void TryGetPackageVersion_reads_version_attribute()
    {
        var version = CsprojPackageReferenceEditor.TryGetPackageVersion(
            SampleCsproj,
            "Observables.RestAPI.R3");

        Assert.Equal("0.1.0-preview4", version);
    }
}
