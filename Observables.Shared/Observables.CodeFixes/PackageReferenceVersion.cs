using System.Reflection;
using System.Xml;
using Microsoft.CodeAnalysis;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

internal static class PackageReferenceVersion
{
    const string CentralProperty = "ManagePackageVersionsCentrally";

    internal static string? DefaultVersion { get; } =
        typeof(PackageReferenceVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "ObservablesPackageVersion")?.Value;

    internal static bool TryResolve(Project project, string content, string packageId, out string? version)
    {
        version = null;
        var projectRoot = CsprojPackageReferenceEditor.LoadDocument(content).DocumentElement!;
        var centralRoot = FindDirectoryProps(project.FilePath!, "Directory.Packages.props");

        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (options.TryGetValue("build_property." + CentralProperty, out var evaluated))
        {
            if (!bool.TryParse(evaluated, out var centrallyManaged))
                return false;

            if (centrallyManaged)
                return HasCentralVersion(projectRoot, packageId) || HasCentralVersion(centralRoot, packageId);
        }
        else
        {
            if (!TryReadCentralProperty(projectRoot, out var projectValue))
                return false;

            if (projectValue is null)
            {
                var buildRoot = FindDirectoryProps(project.FilePath!, "Directory.Build.props");
                if (!TryReadCentralProperty(buildRoot, out var buildValue)
                    || !TryReadCentralProperty(centralRoot, out var centralValue))
                {
                    return false;
                }

                projectValue = centralValue ?? buildValue;
            }

            if (projectValue == true)
                return HasCentralVersion(projectRoot, packageId) || HasCentralVersion(centralRoot, packageId);
        }

        version = InferSiblingVersion(content, packageId) ?? DefaultVersion;
        return !string.IsNullOrWhiteSpace(version);
    }

    internal static string? InferSiblingVersion(string content, string packageId)
    {
        if (!ProxyDomainTable.TryGetDomainByBackendPackageId(packageId, out var domain))
            return null;

        var siblingVersions = domain.BackendPackageIds
            .Where(id => !string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase))
            .Select(id => CsprojPackageReferenceEditor.TryGetPackageVersion(content, id))
            .Where(version => version is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return siblingVersions.Length == 1 ? siblingVersions[0] : null;
    }

    static XmlElement? FindDirectoryProps(string projectPath, string fileName)
    {
        var path = Path.GetDirectoryName(projectPath);
        for (var directory = path is null ? null : new DirectoryInfo(path);
            directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
                return CsprojPackageReferenceEditor.LoadDocument(File.ReadAllText(candidate)).DocumentElement;
        }

        return null;
    }

    static bool TryReadCentralProperty(XmlElement? root, out bool? value)
    {
        value = null;
        if (root is null)
            return true;

        foreach (var group in CsprojPackageReferenceEditor.ChildElements(root, "PropertyGroup"))
        {
            foreach (var property in CsprojPackageReferenceEditor.ChildElements(group, CentralProperty))
            {
                if (CsprojPackageReferenceEditor.Condition(group).Length > 0
                    || CsprojPackageReferenceEditor.Condition(property).Length > 0
                    || !bool.TryParse(property.InnerText, out var literal))
                {
                    return false;
                }

                value = literal;
            }
        }

        return true;
    }

    static bool HasCentralVersion(XmlElement? root, string packageId) =>
        root is not null && CsprojPackageReferenceEditor.ChildElements(root, "ItemGroup")
            .Where(group => CsprojPackageReferenceEditor.Condition(group).Length == 0)
            .SelectMany(group => CsprojPackageReferenceEditor.ChildElements(group, "PackageVersion"))
            .Any(item => CsprojPackageReferenceEditor.Condition(item).Length == 0
                && string.Equals(item.GetAttribute("Include"), packageId, StringComparison.OrdinalIgnoreCase)
                && CsprojPackageReferenceEditor.ReadVersion(item, "Version") is not null);
}
