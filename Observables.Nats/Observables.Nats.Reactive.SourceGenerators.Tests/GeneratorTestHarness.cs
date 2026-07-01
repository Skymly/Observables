using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Nats.Reactive.SourceGenerators;

namespace Observables.Nats.Reactive.SourceGenerators.Tests;

internal static class GeneratorTestHarness
{
    internal static string BuildHarnessDocument(string userSource) =>
        $$"""
        #nullable enable
        using System;
        using System.Threading;
        using Observables.Nats;
        using NATS.Client.Core;

        {{userSource}}
        """;

    internal static GeneratorRunOutput Run(string userSource)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(BuildHarnessDocument(userSource), parseOptions);

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new NatsInterfaceStubGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        var runResult = driver.GetRunResult();
        foreach (var gr in runResult.Results)
        {
            if (gr.Exception is not null)
            {
                throw gr.Exception;
            }
        }

        var generatedSources = runResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .OrderBy(static s => s.HintName, StringComparer.Ordinal)
            .Select(static s => new GeneratedSource(s.HintName, s.SourceText.ToString()))
            .ToImmutableArray();

        var allDiagnostics = runResult.Diagnostics
            .AddRange(driverDiagnostics)
            .AddRange(outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error));

        return new GeneratorRunOutput(generatedSources, allDiagnostics);
    }

    /// <summary>
    /// Runs the generator with incremental step tracking enabled, returning the driver
    /// for a second run. The returned <see cref="CacheTrackingDriver"/> exposes both the
    /// initial driver and the compilation so tests can perform a second run and inspect
    /// <see cref="GeneratorRunResult.TrackedSteps"/>.
    /// </summary>
    internal static CacheTrackingDriver RunWithCacheTracking(string userSource)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(BuildHarnessDocument(userSource), parseOptions);

        var references = GetMetadataReferences();
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new NatsInterfaceStubGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();
        foreach (var gr in runResult.Results)
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

    internal static string ToSnapshot(GeneratorRunOutput output)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Diagnostics:");
        var diagnostics = output.Diagnostics
            .Where(static d => d.Id.StartsWith("OBS9", StringComparison.Ordinal))
            .GroupBy(static d => (d.Id, d.GetMessage()))
            .Select(static g => g.First())
            .OrderBy(static d => d.Id, StringComparer.Ordinal);

        foreach (var diagnostic in diagnostics)
        {
            sb.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        if (!diagnostics.Any())
        {
            sb.AppendLine("  <none>");
        }

        sb.AppendLine();
        sb.AppendLine("Generated Sources:");
        foreach (var source in output.GeneratedSources)
        {
            sb.AppendLine($"--- {source.HintName} ---");
            sb.AppendLine(source.Source.TrimEnd());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    static MetadataReference[] GetMetadataReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var platform = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToList<MetadataReference>();

        AddReference(platform, typeof(global::System.Reactive.Unit).Assembly.Location);
        AddReference(platform, typeof(global::Observables.Nats.NatsService).Assembly.Location);
        AddReference(platform, typeof(global::Observables.Nats.Reactive.SystemReactiveNatsAdapter).Assembly.Location);
        AddReference(platform, typeof(global::NATS.Client.Core.NatsConnection).Assembly.Location);
        return platform.ToArray();
    }

    static void AddReference(List<MetadataReference> references, string path)
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
    /// Adds an unrelated (non-Nats) syntax tree to the compilation for cache testing.
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
