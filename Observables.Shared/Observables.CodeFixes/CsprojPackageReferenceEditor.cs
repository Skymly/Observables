using System.Text.RegularExpressions;

namespace Observables.CodeFixes;

internal static class CsprojPackageReferenceEditor
{
    static readonly Regex PackageReferencePattern = new(
        "<PackageReference\\s+Include\\s*=\\s*\"(?<id>[^\"]+)\"(?:\\s+Version\\s*=\\s*\"(?<version>[^\"]+)\")?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool ContainsPackageReference(string csprojContent, string packageId)
    {
        foreach (Match match in PackageReferencePattern.Matches(csprojContent))
        {
            if (string.Equals(match.Groups["id"].Value, packageId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string? TryGetPackageVersion(string csprojContent, string packageId)
    {
        foreach (Match match in PackageReferencePattern.Matches(csprojContent))
        {
            if (!string.Equals(match.Groups["id"].Value, packageId, StringComparison.OrdinalIgnoreCase))
                continue;

            var version = match.Groups["version"].Value;
            return string.IsNullOrEmpty(version) ? null : version;
        }

        return null;
    }

    public static string AddPackageReferenceIfMissing(string csprojContent, string packageId, string? version)
    {
        if (ContainsPackageReference(csprojContent, packageId))
            return csprojContent;

        var packageLine = version is null
            ? $"    <PackageReference Include=\"{packageId}\" />"
            : $"    <PackageReference Include=\"{packageId}\" Version=\"{version}\" />";

        var lastPackageReference = csprojContent.LastIndexOf("<PackageReference", StringComparison.OrdinalIgnoreCase);
        if (lastPackageReference >= 0)
        {
            var itemGroupEnd = csprojContent.IndexOf("</ItemGroup>", lastPackageReference, StringComparison.OrdinalIgnoreCase);
            if (itemGroupEnd >= 0)
                return csprojContent.Insert(itemGroupEnd, "\n" + packageLine);
        }

        var projectEnd = csprojContent.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
        if (projectEnd < 0)
            throw new InvalidOperationException("The project file does not contain a </Project> element.");

        var block = "\n  <ItemGroup>\n" + packageLine + "\n  </ItemGroup>\n";
        return csprojContent.Insert(projectEnd, block);
    }

    public static string ReplacePackageReference(string csprojContent, string oldPackageId, string newPackageId, string? version)
    {
        if (ContainsPackageReference(csprojContent, newPackageId))
            return RemovePackageReference(csprojContent, oldPackageId);

        var updated = Regex.Replace(
            csprojContent,
            $"(<PackageReference\\s+Include\\s*=\\s*\"){Regex.Escape(oldPackageId)}(\")",
            match => $"{match.Groups[1].Value}{newPackageId}{match.Groups[2].Value}",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!string.Equals(updated, csprojContent, StringComparison.Ordinal))
        {
            if (version is not null)
                updated = SetPackageVersion(updated, newPackageId, version);

            return updated;
        }

        return AddPackageReferenceIfMissing(RemovePackageReference(csprojContent, oldPackageId), newPackageId, version);
    }

    public static string RemovePackageReference(string csprojContent, string packageId)
    {
        return Regex.Replace(
            csprojContent,
            "\\s*<PackageReference\\s+Include\\s*=\\s*\"" + Regex.Escape(packageId) +
            "\"(?:\\s+Version\\s*=\\s*\"[^\"]+\")?\\s*/>\\s*",
            "\n",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    static string SetPackageVersion(string csprojContent, string packageId, string version)
    {
        var pattern =
            "(<PackageReference\\s+Include\\s*=\\s*\"" + Regex.Escape(packageId) +
            "\")(?:\\s+Version\\s*=\\s*\"[^\"]+\")?(\\s*/>)";

        return Regex.Replace(
            csprojContent,
            pattern,
            $"$1 Version=\"{version}\"$2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
