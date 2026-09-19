using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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

        IReadOnlyList<string> declared = ReadDeclaredFrameworks(projectPath);
        if (declared.Count > 0)
        {
            return declared;
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

    static IReadOnlyList<string> ReadDeclaredFrameworks(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return Array.Empty<string>();
        }

        XDocument document = XDocument.Load(projectPath);
        foreach (XElement propertyGroup in document.Root?.Elements("PropertyGroup") ?? Enumerable.Empty<XElement>())
        {
            if (propertyGroup.Attribute("Condition") is not null)
            {
                continue;
            }

            string? frameworks = (string?)propertyGroup.Element("TargetFrameworks");
            if (!string.IsNullOrWhiteSpace(frameworks))
            {
                return frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            string? framework = (string?)propertyGroup.Element("TargetFramework");
            if (!string.IsNullOrWhiteSpace(framework))
            {
                return new[] { framework.Trim() };
            }
        }

        return Array.Empty<string>();
    }
}
