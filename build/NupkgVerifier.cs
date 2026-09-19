using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

static class NupkgVerifier
{
    public const string AnalyzerFolder = "analyzers/dotnet/roslyn4.12/cs/";

    public static IReadOnlyList<string> Verify(string nupkgPath, NupkgVerifyRequest request)
    {
        var errors = new List<string>();

        using ZipArchive zip = ZipFile.OpenRead(nupkgPath);
        HashSet<string> entries = zip.Entries
            .Select(static e => e.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string generatorPath = AnalyzerFolder + request.GeneratorAssemblyFileName;
        if (!entries.Contains(generatorPath))
        {
            errors.Add($"{request.PackageId}: missing generator DLL {generatorPath}");
        }

        if (!entries.Contains(AnalyzerFolder + "Observables.CodeFixes.dll"))
        {
            errors.Add($"{request.PackageId}: missing Observables.CodeFixes.dll under {AnalyzerFolder}");
        }

        if (!entries.Contains(AnalyzerFolder + "Observables.Analyzers.dll"))
        {
            errors.Add($"{request.PackageId}: missing Observables.Analyzers.dll under {AnalyzerFolder}");
        }

        string propsFileName = request.PackageId + ".props";
        if (!entries.Contains("build/" + propsFileName))
        {
            errors.Add($"{request.PackageId}: missing build/{propsFileName}");
        }

        if (!entries.Contains("buildTransitive/" + propsFileName))
        {
            errors.Add($"{request.PackageId}: missing buildTransitive/{propsFileName}");
        }

        foreach (string tfm in request.RequiredLibTfms)
        {
            string prefix = "lib/" + tfm + "/";
            bool hasDll = entries.Any(e =>
                e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            if (!hasDll)
            {
                errors.Add($"{request.PackageId}: missing runtime assembly under lib/{tfm}/");
            }
        }

        if (request.ForbiddenLibAssemblyReferences.Count > 0)
        {
            errors.AddRange(FindForbiddenLibAssemblyReferences(zip, request));
        }

        if (request.RequireReadme && !entries.Contains("README.md"))
        {
            errors.Add($"{request.PackageId}: missing package README.md at package root");
        }
        else if (request.RequireReadme)
        {
            ZipArchiveEntry? readmeEntry = zip.Entries.FirstOrDefault(static e =>
                string.Equals(e.FullName.Replace('\\', '/'), "README.md", StringComparison.OrdinalIgnoreCase));
            if (readmeEntry is not null)
            {
                using Stream readmeStream = readmeEntry.Open();
                using var readmeReader = new StreamReader(readmeStream);
                if (ReadmePinsObservablesPackageVersion(readmeReader.ReadToEnd()))
                {
                    errors.Add(
                        $"{request.PackageId}: README.md must not pin Observables.* PackageReference Version");
                }
            }
        }

        ZipArchiveEntry? nuspecEntry = zip.Entries.FirstOrDefault(static e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            errors.Add($"{request.PackageId}: missing .nuspec");
            return errors;
        }

        using Stream nuspecStream = nuspecEntry.Open();
        using var nuspecReader = new StreamReader(nuspecStream);
        string nuspecText = nuspecReader.ReadToEnd();

        foreach (string forbidden in request.ForbiddenNuspecSubstrings)
        {
            bool inNuspec = nuspecText.Contains(forbidden, StringComparison.OrdinalIgnoreCase);
            bool inEntries = entries.Any(e => e.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            if (inNuspec || inEntries)
            {
                errors.Add($"{request.PackageId}: {forbidden} must stay out of pack dependency graphs");
            }
        }

        Dictionary<string, HashSet<string>> groups = ParseNuspecDependencyGroups(nuspecText);
        foreach (string forbiddenId in request.ForbiddenNuspecDependencyIds)
        {
            foreach ((string tfm, HashSet<string> ids) in groups)
            {
                if (ids.Contains(forbiddenId))
                {
                    errors.Add($"{request.PackageId}: nuspec {tfm} group must not depend on {forbiddenId}");
                }
            }
        }

        foreach ((string tfm, IReadOnlyList<string> requiredIds) in request.RequiredDependenciesByTfm)
        {
            HashSet<string> actual = ResolveGroup(groups, tfm);
            foreach (string id in requiredIds)
            {
                if (!actual.Contains(id))
                {
                    errors.Add($"{request.PackageId}: nuspec {tfm} group missing dependency {id}");
                }
            }
        }

        return errors;
    }

    static IEnumerable<string> FindForbiddenLibAssemblyReferences(ZipArchive zip, NupkgVerifyRequest request)
    {
        var forbidden = new HashSet<string>(request.ForbiddenLibAssemblyReferences, StringComparer.OrdinalIgnoreCase);

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (!path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream stream = entry.Open();
            foreach (string reference in ReadAssemblyReferences(stream))
            {
                if (forbidden.Contains(reference))
                {
                    yield return $"{request.PackageId}: {path} references {reference}";
                }
            }
        }
    }

    internal static IReadOnlyList<string> ReadAssemblyReferences(Stream assembly)
    {
        using var buffer = new MemoryStream();
        assembly.CopyTo(buffer);
        buffer.Position = 0;

        using var pe = new PEReader(buffer);
        if (!pe.HasMetadata)
        {
            return [];
        }

        MetadataReader metadata = pe.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToArray();
    }

    internal static bool ReadmePinsObservablesPackageVersion(string readme)
    {
        foreach (string line in readme.Split('\n'))
        {
            if (LineIncludesObservablesPackage(line) && LineHasVersionAttribute(line))
            {
                return true;
            }
        }

        return false;
    }

    static bool LineIncludesObservablesPackage(string line)
    {
        return line.Contains("Include=\"Observables.", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Include='Observables.", StringComparison.OrdinalIgnoreCase);
    }

    static bool LineHasVersionAttribute(string line)
    {
        return line.Contains("Version=", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Version =", StringComparison.OrdinalIgnoreCase);
    }

    static Dictionary<string, HashSet<string>> ParseNuspecDependencyGroups(string nuspecText)
    {
        var groups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        XDocument doc = XDocument.Parse(nuspecText);
        foreach (XElement group in doc.Descendants().Where(static e => e.Name.LocalName == "group"))
        {
            string tfm = (string?)group.Attribute("targetFramework") ?? string.Empty;
            HashSet<string> ids = groups.GetValueOrDefault(tfm) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            groups[tfm] = ids;
            foreach (XElement dependency in group.Elements().Where(static e => e.Name.LocalName == "dependency"))
            {
                string? id = (string?)dependency.Attribute("id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        return groups;
    }

    static HashSet<string> ResolveGroup(Dictionary<string, HashSet<string>> groups, string tfm)
    {
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (groups.TryGetValue(string.Empty, out HashSet<string>? any))
        {
            actual.UnionWith(any);
        }

        foreach ((string key, HashSet<string> ids) in groups)
        {
            if (TfmsMatch(key, tfm))
            {
                actual.UnionWith(ids);
            }
        }

        return actual;
    }

    internal static bool TfmsMatch(string left, string right)
    {
        return string.Equals(NormalizeTfm(left), NormalizeTfm(right), StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeTfm(string tfm)
    {
        string value = tfm.Trim().TrimStart('.');
        if (value.StartsWith("NETStandard", StringComparison.OrdinalIgnoreCase))
        {
            value = "netstandard" + value["NETStandard".Length..];
        }
        else if (value.StartsWith("NETCoreApp", StringComparison.OrdinalIgnoreCase))
        {
            value = "net" + value["NETCoreApp".Length..];
        }

        return value.ToLowerInvariant();
    }
}
