using System.Text.RegularExpressions;
using System.Xml.Linq;

static partial class PackCsprojReader
{
    public static readonly string[] DefaultLibTfms =
    [
        "netstandard2.0",
        "net8.0",
        "net9.0",
        "net10.0",
    ];

    static readonly HashSet<string> BackendPackageIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "R3",
        "System.Reactive",
    };

    /// <summary>
    /// Domains whose runtime assembly still carries the R3 bridges, so their Reactive package still ships a
    /// DLL that references R3. See ADR-003; delete a name when its split lands, and delete this set when it
    /// empties.
    /// </summary>
    internal static readonly HashSet<string> DomainsPendingBackendSplit = new(StringComparer.OrdinalIgnoreCase)
    {
        "Grpc",
    };

    public static NupkgVerifyRequest FromPackProject(string packCsprojPath)
    {
        XDocument pack = XDocument.Load(packCsprojPath);
        string packDir = Path.GetDirectoryName(packCsprojPath)
            ?? throw new InvalidOperationException($"Pack project has no directory: {packCsprojPath}");

        string packageId = RequiredProperty(pack, "PackageId", packCsprojPath);
        string generatorAssembly = RequiredProperty(pack, "ObservablesPackGeneratorAssemblyName", packCsprojPath);

        string[] libProjects = pack.Descendants("ObservablesPackLibProjectReference")
            .Select(static e => (string?)e.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ResolveIncludePath(packDir, include!))
            .ToArray();

        string[] libTfms = ReadLibTfms(libProjects);

        string[] tfmsForDeps = libTfms.Length > 0 ? libTfms : ["netstandard2.0"];
        var depsByTfm = tfmsForDeps.ToDictionary(
            static tfm => tfm,
            static _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (PackageRef reference in ReadPackageReferences(pack))
        {
            if (reference.IsPrivateAssetsAll)
            {
                continue;
            }

            foreach (string tfm in tfmsForDeps)
            {
                if (reference.AppliesTo(tfm))
                {
                    AddUnique(depsByTfm[tfm], reference.Id);
                }
            }
        }

        foreach (string libPath in libProjects)
        {
            XDocument lib = XDocument.Load(libPath);
            foreach (PackageRef reference in ReadPackageReferences(lib))
            {
                if (reference.IsPrivateAssetsAll || BackendPackageIds.Contains(reference.Id))
                {
                    continue;
                }

                foreach (string tfm in tfmsForDeps)
                {
                    if (reference.AppliesTo(tfm))
                    {
                        AddUnique(depsByTfm[tfm], reference.Id);
                    }
                }
            }
        }

        bool isRedis = packageId.StartsWith("Observables.Redis.", StringComparison.Ordinal);

        return new NupkgVerifyRequest
        {
            PackageId = packageId,
            GeneratorAssemblyFileName = generatorAssembly,
            RequiredLibTfms = libTfms,
            RequiredDependenciesByTfm = depsByTfm.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            ForbiddenNuspecSubstrings = isRedis ? ["Garnet"] : [],
            ForbiddenLibAssemblyReferences = ForbiddenBackendAssemblies(packageId),
        };
    }

    /// <summary>
    /// The backend a package must not ship, per AGENTS.md: an R3 package carries no System.Reactive and a
    /// Reactive package carries no R3. Returns nothing for a domain that ADR-003 has not split yet.
    /// </summary>
    internal static string[] ForbiddenBackendAssemblies(string packageId)
    {
        string[] parts = packageId.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], "Observables", StringComparison.Ordinal))
        {
            return [];
        }

        if (DomainsPendingBackendSplit.Contains(parts[1]))
        {
            return [];
        }

        return parts[2] switch
        {
            "R3" => ["System.Reactive"],
            "Reactive" => ["R3"],
            _ => [],
        };
    }

    static string[] ReadLibTfms(string[] libProjects)
    {
        if (libProjects.Length == 0)
        {
            return [];
        }

        var declared = new List<string>();
        foreach (string libPath in libProjects)
        {
            XDocument lib = XDocument.Load(libPath);
            string? frameworks = Property(lib, "TargetFrameworks");
            if (!string.IsNullOrWhiteSpace(frameworks))
            {
                declared.AddRange(frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                continue;
            }

            string? framework = Property(lib, "TargetFramework");
            if (!string.IsNullOrWhiteSpace(framework))
            {
                declared.Add(framework);
            }
        }

        if (declared.Count == 0)
        {
            return DefaultLibTfms;
        }

        return declared
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static IEnumerable<PackageRef> ReadPackageReferences(XDocument document)
    {
        foreach (XElement itemGroup in document.Descendants("ItemGroup"))
        {
            string? condition = (string?)itemGroup.Attribute("Condition");
            foreach (XElement reference in itemGroup.Elements("PackageReference"))
            {
                string? id = (string?)reference.Attribute("Include") ?? (string?)reference.Attribute("Update");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                string? privateAssets = (string?)reference.Attribute("PrivateAssets")
                    ?? reference.Element("PrivateAssets")?.Value;
                yield return new PackageRef(id, privateAssets, condition);
            }
        }
    }

    internal static string ResolveIncludePath(string baseDirectory, string include)
    {
        string normalized = include.Replace('\\', '/');
        return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
    }

    static string RequiredProperty(XDocument document, string name, string path)
    {
        return Property(document, name)
            ?? throw new InvalidOperationException($"{name} missing in {path}");
    }

    static string? Property(XDocument document, string name) =>
        document.Descendants("PropertyGroup")
            .Elements(name)
            .Select(static e => e.Value.Trim())
            .FirstOrDefault(static value => value.Length > 0);

    static void AddUnique(List<string> ids, string id)
    {
        if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            ids.Add(id);
        }
    }

    sealed record PackageRef(string Id, string? PrivateAssets, string? Condition)
    {
        public bool IsPrivateAssetsAll =>
            string.Equals(PrivateAssets, "all", StringComparison.OrdinalIgnoreCase);

        public bool AppliesTo(string tfm)
        {
            if (string.IsNullOrWhiteSpace(Condition))
            {
                return true;
            }

            Match match = TargetFrameworkConditionRegex().Match(Condition);
            if (!match.Success)
            {
                return false;
            }

            return string.Equals(match.Groups[1].Value, tfm, StringComparison.OrdinalIgnoreCase);
        }
    }

    [GeneratedRegex(@"'\$\(TargetFramework\)'\s*==\s*'([^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex TargetFrameworkConditionRegex();
}
