using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.RestAPI.R3.SourceGenerators;

namespace Observables.RestAPI.GeneratorTests;

internal static class GeneratorTestHarness
{
    internal static string BuildHarnessDocument(string userSource) =>
        $$"""
        #nullable enable
        using System;
        using System.Collections.Generic;
        using System.Net.Http;
        using System.Threading;
        using System.Threading.Tasks;
        using R3;
        using Observables.RestAPI;

        {{userSource}}
        """;

    internal static (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        MetadataReference[]? extraReferences = null)
    {
        string source = BuildHarnessDocument(userSource);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = GetMetadataReferences();
        if (extraReferences is not null)
        {
            references = references.Concat(extraReferences).ToArray();
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (compilation, syntaxTree);
    }

    public static GeneratorRunOutput Run(
        string userSource,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        IIncrementalGenerator[]? generators = null,
        MetadataReference[]? extraReferences = null)
    {
        (CSharpCompilation compilation, _) =
            CreateHarnessCompilation(userSource, languageVersion, extraReferences);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);

        IIncrementalGenerator[] useGenerators = generators ??
        [
            new RestApiR3InterfaceStubGenerator(),
        ];

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: useGenerators.Select(static g => g.AsSourceGenerator()),
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        ImmutableArray<Diagnostic> compilationErrors = outputCompilation
            .GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        foreach (GeneratorRunResult gr in runResult.Results)
        {
            if (gr.Exception is not null)
            {
                throw gr.Exception;
            }
        }

        ImmutableArray<Diagnostic> generatorDiagnostics = runResult.Diagnostics
            .AddRange(runResult.Results.SelectMany(static r => r.Diagnostics));

        var generatedSources = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .OrderBy(static item => item.HintName, StringComparer.Ordinal)
            .Select(static item => new GeneratedSource(item.HintName, item.SourceText.ToString()))
            .ToImmutableArray();

        return new GeneratorRunOutput(
            generatedSources,
            generatorDiagnostics.AddRange(driverDiagnostics).AddRange(compilationErrors));
    }

    public static string ToSnapshot(GeneratorRunOutput output)
    {
        var sb = new StringBuilder();

        var diagnostics = output.Diagnostics
            .Where(static d => d.Id.StartsWith("OBS3", StringComparison.Ordinal))
            .GroupBy(static d => (d.Id, d.GetMessage()))
            .Select(static g => g.First())
            .OrderBy(static d => d.Id, StringComparer.Ordinal)
            .ThenBy(static d => d.GetMessage(), StringComparer.Ordinal)
            .ToImmutableArray();

        sb.AppendLine("Diagnostics:");
        if (diagnostics.IsDefaultOrEmpty)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                sb.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generated Sources:");

        foreach (GeneratedSource source in output.GeneratedSources)
        {
            sb.AppendLine($"--- {source.HintName} ---");
            sb.AppendLine(source.Source.TrimEnd());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    internal static MetadataReference[] GetMetadataReferences()
    {
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");
        }

        var platform = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToList<MetadataReference>();

        AddAssemblyReference(platform, typeof(global::R3.Unit).Assembly.Location);
        AddAssemblyReference(platform, typeof(global::Observables.RestAPI.RestService).Assembly.Location);
        AddAssemblyReference(platform, typeof(global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter).Assembly.Location);

        return platform.ToArray();
    }

    static void AddAssemblyReference(List<MetadataReference> references, string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required test reference not found: {path}");
        }

        references.Add(MetadataReference.CreateFromFile(path));
    }
}

internal sealed record GeneratorRunOutput(
    ImmutableArray<GeneratedSource> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics);

internal sealed record GeneratedSource(string HintName, string Source);
