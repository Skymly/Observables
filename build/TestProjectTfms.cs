using System;
using System.Collections.Generic;
using System.IO;

internal static class TestProjectTfms
{
    public static bool IsTrimAnalysisProject(string projectPath)
    {
        return string.Equals(
            Path.GetFileName(projectPath),
            "Observables.TrimTests.csproj",
            StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> GetFrameworks(string projectPath)
    {
        if (IsTrimAnalysisProject(projectPath))
        {
            return Array.Empty<string>();
        }

        string name = Path.GetFileNameWithoutExtension(projectPath);
        bool singleTfm = name.Contains("SourceGenerators", StringComparison.Ordinal)
            || string.Equals(name, "Observables.RestAPI.GeneratorTests", StringComparison.Ordinal)
            || string.Equals(name, "Observables.CodeFixes.Tests", StringComparison.Ordinal)
            || string.Equals(name, "Observables.Analyzers.Tests", StringComparison.Ordinal)
            || string.Equals(name, "Observables.TestSupport.Tests", StringComparison.Ordinal)
            || string.Equals(name, "Observables.Build.Tests", StringComparison.Ordinal);

        return singleTfm
            ? new[] { "net8.0" }
            : new[] { "net8.0", "net9.0", "net10.0" };
    }

    public static string TrxFileName(string projectPath, string tfm) =>
        Path.GetFileNameWithoutExtension(projectPath) + "." + tfm + ".trx";
}
