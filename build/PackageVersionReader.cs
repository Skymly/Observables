using System.Text.RegularExpressions;

using Nuke.Common;
using Nuke.Common.IO;

static partial class PackageVersionReader
{
    [GeneratedRegex("<PackageVersion>([^<]+)</PackageVersion>", RegexOptions.CultureInvariant)]
    private static partial Regex PackageVersionRegex();

    [GeneratedRegex("<Version>([^<]+)</Version>", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    public static string ReadFromProps(AbsolutePath propsFile)
    {
        Assert.FileExists(propsFile, $"Package props not found: {propsFile}");

        string content = File.ReadAllText(propsFile);

        Match packageVersionMatch = PackageVersionRegex().Match(content);
        if (packageVersionMatch.Success)
        {
            return packageVersionMatch.Groups[1].Value.Trim();
        }

        Match versionMatch = VersionRegex().Match(content);
        if (versionMatch.Success)
        {
            return versionMatch.Groups[1].Value.Trim();
        }

        throw new InvalidOperationException(
            $"Neither <PackageVersion> nor <Version> found in {propsFile}");
    }
}
