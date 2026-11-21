using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

static class PackCsprojReader
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
        "ReactiveUI.Primitives",
    };

    public static NupkgVerifyRequest FromPackProject(string packCsprojPath, string configuration = "Release")
    {
        XDocument pack = XDocument.Load(packCsprojPath);
        string packDir = Path.GetDirectoryName(packCsprojPath)
            ?? throw new InvalidOperationException($"Pack project has no directory: {packCsprojPath}");

        string packageId = RequiredProperty(pack, "PackageId", packCsprojPath);
        string generatorAssembly = RequiredProperty(pack, "ObservablesPackGeneratorAssemblyName", packCsprojPath);

        LibProject[] libProjects = pack.Descendants("ObservablesPackLibProjectReference")
            .Select(static e => (string?)e.Attribute("Include"))
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(include => ReadLibProject(ResolveIncludePath(packDir, include!)))
            .ToArray();

        var libsByTfm = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var depsByTfm = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        string[] tfms = libProjects.Length == 0
            ? ["netstandard2.0"]
            : libProjects.SelectMany(static project => project.Frameworks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string tfm in tfms)
        {
            var dependencies = new HashSet<string>(ReadPackageReferences(packCsprojPath, tfm, configuration), StringComparer.OrdinalIgnoreCase);
            LibProject[] matchingLibraries = libProjects
                .Where(project => project.Frameworks.Contains(tfm, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            foreach (LibProject lib in matchingLibraries)
            {
                dependencies.UnionWith(ReadPackageReferences(lib.Path, tfm, configuration)
                    .Where(id => !BackendPackageIds.Contains(id)));
            }

            if (matchingLibraries.Length > 0)
            {
                libsByTfm.Add(tfm, matchingLibraries.Select(static library => library.AssemblyFileName).ToArray());
            }

            depsByTfm.Add(tfm, dependencies.ToArray());
        }

        string[] forbiddenBackends = ForbiddenBackendAssemblies(packageId);
        return new NupkgVerifyRequest
        {
            PackageId = packageId,
            GeneratorAssemblyFileName = generatorAssembly,
            RequiredLibAssembliesByTfm = libsByTfm,
            RequiredDependenciesByTfm = depsByTfm,
            ForbiddenNuspecSubstrings = packageId.StartsWith("Observables.Redis.", StringComparison.Ordinal) ? ["Garnet"] : [],
            ForbiddenNuspecDependencyIds = forbiddenBackends,
            ForbiddenLibAssemblyReferences = forbiddenBackends,
        };
    }

    internal static string[] ForbiddenBackendAssemblies(string packageId)
    {
        string[] parts = packageId.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], "Observables", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return parts[2].ToUpperInvariant() switch
        {
            "R3" => ["System.Reactive", "ReactiveUI.Primitives"],
            "REACTIVE" => ["R3", "ReactiveUI.Primitives"],
            "PRIMITIVES" => ["R3", "System.Reactive"],
            _ => [],
        };
    }

    static LibProject ReadLibProject(string path)
    {
        XDocument project = XDocument.Load(path);
        string assembly = Property(project, "AssemblyName") ?? Path.GetFileNameWithoutExtension(path);
        string? frameworks = Property(project, "TargetFrameworks") ?? Property(project, "TargetFramework");
        return new LibProject(path, assembly + ".dll", frameworks is null
            ? DefaultLibTfms
            : frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    static string[] ReadPackageReferences(string path, string tfm, string configuration)
    {
        // MSBuild evaluates both item and item-group conditions, including imports and item updates.
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(Path.GetFullPath(path));
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-getItem:PackageReference");
        startInfo.ArgumentList.Add($"-p:TargetFramework={tfm}");
        startInfo.ArgumentList.Add($"-p:Configuration={configuration}");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start MSBuild to evaluate package references.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"MSBuild could not evaluate package references for {path} ({tfm}): {errors.GetAwaiter().GetResult()}");
        }

        using JsonDocument evaluated = JsonDocument.Parse(output.GetAwaiter().GetResult());
        return evaluated.RootElement.GetProperty("Items").GetProperty("PackageReference").EnumerateArray()
            .Where(static item => !item.TryGetProperty("PrivateAssets", out JsonElement assets)
                || !string.Equals(assets.GetString(), "all", StringComparison.OrdinalIgnoreCase))
            .Select(static item => item.GetProperty("Identity").GetString()!)
            .ToArray();
    }

    internal static string ResolveIncludePath(string baseDirectory, string include)
    {
        string normalized = include.Replace('\\', '/');
        return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
    }

    static string RequiredProperty(XDocument document, string name, string path) =>
        Property(document, name)
        ?? throw new InvalidOperationException($"{name} missing in {path}");

    static string? Property(XDocument document, string name) =>
        document.Descendants("PropertyGroup")
            .Elements(name)
            .Select(static e => e.Value.Trim())
            .FirstOrDefault(static value => value.Length > 0);

    sealed record LibProject(string Path, string AssemblyFileName, string[] Frameworks);
}
