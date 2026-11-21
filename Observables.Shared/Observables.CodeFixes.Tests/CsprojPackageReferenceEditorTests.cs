namespace Observables.CodeFixes.Tests;

public sealed class CsprojPackageReferenceEditorTests
{
    const string SampleCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Observables.RestAPI.R3" Version="0.1.1" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void AddPackageReferenceIfMissing_appends_to_existing_item_group()
    {
        var updated = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            SampleCsproj,
            "Observables.RestAPI",
            version: "0.1.1");

        Assert.Contains("<PackageReference Include=\"Observables.RestAPI\" Version=\"0.1.1\" />", updated);
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
            version: "0.1.1");

        Assert.DoesNotContain("Observables.RestAPI.R3", updated);
        Assert.Contains("<PackageReference Include=\"Observables.RestAPI.Reactive\" Version=\"0.1.1\" />", updated);
    }

    [Fact]
    public void AddPackageReferenceIfMissing_uses_a_project_wide_group()
    {
        const string project = """
            <Project>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="R3" Version="1.3.0" />
              </ItemGroup>
            </Project>
            """;

        var updated = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            project, "Observables.Mqtt.R3", "0.4.1");

        var document = System.Xml.Linq.XDocument.Parse(updated);
        var addedGroup = Assert.Single(document.Root!.Elements("ItemGroup"),
            group => group.Elements("PackageReference").Any(reference =>
                (string?)reference.Attribute("Include") == "Observables.Mqtt.R3"));
        Assert.Null(addedGroup.Attribute("Condition"));
    }

    [Fact]
    public void AddPackageReferenceIfMissing_preserves_existing_package_id_spelling()
    {
        var updated = CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
            SampleCsproj.Replace("Observables.RestAPI.R3", "observables.restapi.r3", StringComparison.Ordinal),
            "Observables.RestAPI.R3",
            "0.4.1");

        Assert.DoesNotContain("Include=\"Observables.RestAPI.R3\"", updated, StringComparison.Ordinal);
        Assert.Contains("Include=\"observables.restapi.r3\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplacePackageReference_switches_all_conditioned_references_with_their_versions()
    {
        const string project = """
            <Project>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="observables.restapi.r3" Version="0.4.0" />
              </ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
                <PackageReference Include="Observables.RestAPI.R3">
                  <Version>0.4.1</Version>
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;

        var updated = CsprojPackageReferenceEditor.ReplacePackageReference(
            project, "Observables.RestAPI.R3", "Observables.RestAPI.Reactive", version: null);

        var groups = System.Xml.Linq.XDocument.Parse(updated).Root!.Elements("ItemGroup").ToArray();
        Assert.Equal(2, groups.Length);
        var net8 = Assert.Single(groups[0].Elements("PackageReference"));
        var net10 = Assert.Single(groups[1].Elements("PackageReference"));
        Assert.Equal("'$(TargetFramework)' == 'net8.0'", (string?)groups[0].Attribute("Condition"));
        Assert.Equal("'$(TargetFramework)' == 'net10.0'", (string?)groups[1].Attribute("Condition"));
        Assert.Equal("Observables.RestAPI.Reactive", (string?)net8.Attribute("Include"));
        Assert.Equal("Observables.RestAPI.Reactive", (string?)net10.Attribute("Include"));
        Assert.Equal("0.4.0", (string?)net8.Attribute("Version"));
        Assert.Equal("0.4.1", (string?)net10.Element("Version"));
        Assert.Equal("all", (string?)net10.Element("PrivateAssets"));
    }

    [Fact]
    public void TryGetPackageVersion_reads_version_attribute()
    {
        var version = CsprojPackageReferenceEditor.TryGetPackageVersion(
            SampleCsproj,
            "Observables.RestAPI.R3");

        Assert.Equal("0.1.1", version);
    }
}
