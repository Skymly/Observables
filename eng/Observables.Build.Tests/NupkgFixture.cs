using System.IO.Compression;
using System.Text;

namespace Observables.Build.Tests;

static class NupkgFixture
{
    public static string Create(
        string packageId,
        IEnumerable<string> files,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? nuspecDependenciesByTfm = null,
        string? extraNuspecText = null,
        IReadOnlyDictionary<string, string>? fileContents = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"obs-nupkg-{Guid.NewGuid():N}.nupkg");
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (string file in files)
        {
            string normalized = file.Replace('\\', '/').TrimStart('/');
            ZipArchiveEntry entry = zip.CreateEntry(normalized, CompressionLevel.NoCompression);
            using Stream stream = entry.Open();
            string content = "x";
            if (fileContents is not null
                && fileContents.TryGetValue(normalized, out string? custom)
                && custom is not null)
            {
                content = custom;
            }

            stream.Write(Encoding.UTF8.GetBytes(content));
        }

        string nuspec = BuildNuspec(packageId, nuspecDependenciesByTfm, extraNuspecText);
        ZipArchiveEntry nuspecEntry = zip.CreateEntry($"{packageId}.nuspec", CompressionLevel.NoCompression);
        using (Stream stream = nuspecEntry.Open())
        {
            stream.Write(Encoding.UTF8.GetBytes(nuspec));
        }

        return path;
    }

    static string BuildNuspec(
        string packageId,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? dependenciesByTfm,
        string? extraNuspecText)
    {
        var groups = new StringBuilder();
        if (dependenciesByTfm is not null)
        {
            foreach ((string tfm, IReadOnlyList<string> ids) in dependenciesByTfm)
            {
                groups.Append("      <group targetFramework=\"").Append(Xml(tfm)).AppendLine("\">");
                foreach (string id in ids)
                {
                    groups.Append("        <dependency id=\"").Append(Xml(id)).AppendLine("\" version=\"1.0.0\" />");
                }

                groups.AppendLine("      </group>");
            }
        }

        return
            """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>
            """
            + Xml(packageId)
            + """
            </id>
                <version>1.0.0</version>
                <authors>test</authors>
                <description>test</description>
                <dependencies>
            """
            + groups
            + """
                </dependencies>
              </metadata>
            """
            + extraNuspecText
            + """
            </package>
            """;
    }

    static string Xml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
