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

    /// <summary>
    /// Runs the generator with incremental step tracking enabled, returning the driver
    /// for a second run. The returned <see cref="CacheTrackingDriver"/> exposes both the
    /// initial driver and the compilation so tests can perform a second run and inspect
    /// <see cref="GeneratorRunResult.TrackedSteps"/>.
    /// </summary>
    internal static CacheTrackingDriver RunWithCacheTracking(string userSource)
    {
        (CSharpCompilation compilation, _) = CreateHarnessCompilation(userSource);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new RestApiR3InterfaceStubGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();
        foreach (GeneratorRunResult gr in runResult.Results)
        {
            if (gr.Exception is not null)
            {
                throw gr.Exception;
            }
        }

        return new CacheTrackingDriver(driver, compilation, parseOptions);
    }

    /// <summary>
    /// Gets the <see cref="IncrementalStepRunReason"/> for a tracked step from the most recent run.
    /// TrackedSteps accumulates entries across runs; this returns the last (most recent) entry.
    /// </summary>
    internal static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName)
    {
        if (!result.TrackedSteps.TryGetValue(stepName, out var steps))
            throw new InvalidOperationException($"Tracked step '{stepName}' not found. Available: [{string.Join(", ", result.TrackedSteps.Keys)}]");

        // TrackedSteps accumulates across runs; take the last entry for the most recent run.
        var step = steps[^1];
        return step.Outputs[^1].Reason;
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

/// <summary>
/// Holds the driver and compilation after the first run with cache tracking enabled.
/// Tests call <see cref="RunSecond"/> to perform a second run and inspect tracked steps.
/// </summary>
internal sealed record CacheTrackingDriver(
    GeneratorDriver Driver,
    CSharpCompilation Compilation,
    CSharpParseOptions ParseOptions)
{
    /// <summary>
    /// Runs the generator a second time on the same driver (testing cache hits/misses).
    /// </summary>
    public GeneratorRunResult RunSecond(CSharpCompilation? editedCompilation = null)
    {
        var driver = Driver.RunGenerators(editedCompilation ?? Compilation);
        return driver.GetRunResult().Results[0];
    }

    /// <summary>
    /// Adds an unrelated (non-RestAPI interface) syntax tree to the compilation for cache testing.
    /// </summary>
    public CSharpCompilation WithUnrelatedTree(string source = "class Dummy {}")
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        return Compilation.AddSyntaxTrees(tree);
    }

    /// <summary>
    /// Adds a new syntax tree with the given user source (wrapped in harness boilerplate)
    /// to the compilation for cache testing.
    /// </summary>
    public CSharpCompilation WithAdditionalSource(string userSource)
    {
        var tree = CSharpSyntaxTree.ParseText(
            GeneratorTestHarness.BuildHarnessDocument(userSource),
            ParseOptions);
        return Compilation.AddSyntaxTrees(tree);
    }
};
