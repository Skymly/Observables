using System.Collections.Generic;

sealed class NupkgVerifyRequest
{
    public required string PackageId { get; init; }

    public required string GeneratorAssemblyFileName { get; init; }

    public IReadOnlyList<string> RequiredLibTfms { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredDependenciesByTfm { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ForbiddenNuspecSubstrings { get; init; } = [];

    /// <summary>
    /// Assembly names that no <c>lib/</c> assembly in the package may reference. Catches a dependency that
    /// hides inside a shipped DLL instead of showing up in the nuspec.
    /// </summary>
    public IReadOnlyList<string> ForbiddenLibAssemblyReferences { get; init; } = [];

    public bool RequireReadme { get; init; } = true;
}
