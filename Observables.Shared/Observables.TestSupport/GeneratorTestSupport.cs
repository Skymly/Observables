using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Observables.TestSupport;

public static class GeneratorTestRunner
{
    public static (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        Func<string, string> buildHarnessDocument,
        IEnumerable<MetadataReference> references,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        string? syntaxTreePath = null)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            buildHarnessDocument(userSource),
            parseOptions,
            path: syntaxTreePath ?? string.Empty);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (compilation, syntaxTree);
    }

    public static GeneratorRunOutput Run(
        string userSource,
        Func<string, string> buildHarnessDocument,
        IEnumerable<MetadataReference> references,
        IEnumerable<IIncrementalGenerator> generators,
        LanguageVersion languageVersion = LanguageVersion.Preview,
        AnalyzerConfigOptionsProvider? optionsProvider = null,
        string? syntaxTreePath = null,
        bool includeResultDiagnostics = false)
    {
        (CSharpCompilation compilation, _) = CreateHarnessCompilation(
            userSource,
            buildHarnessDocument,
            references,
            languageVersion,
            syntaxTreePath);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: generators.Select(static generator => generator.AsSourceGenerator()),
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out ImmutableArray<Diagnostic> driverDiagnostics);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        ThrowGeneratorExceptions(runResult);

        var generatedSources = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .Select(static source => new GeneratedSource(source.HintName, source.SourceText.ToString()))
            .ToImmutableArray();

        ImmutableArray<Diagnostic> diagnostics = runResult.Diagnostics;
        if (includeResultDiagnostics)
        {
            diagnostics = diagnostics.AddRange(runResult.Results.SelectMany(static result => result.Diagnostics));
        }

        diagnostics = diagnostics
            .AddRange(driverDiagnostics)
            .AddRange(outputCompilation.GetDiagnostics().Where(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error));

        return new GeneratorRunOutput(generatedSources, diagnostics);
    }

    public static CacheTrackingDriver RunWithCacheTracking(
        string userSource,
        Func<string, string> buildHarnessDocument,
        IEnumerable<MetadataReference> references,
        IEnumerable<IIncrementalGenerator> generators,
        string? syntaxTreePath = null,
        AnalyzerConfigOptionsProvider? optionsProvider = null)
    {
        (CSharpCompilation compilation, _) = CreateHarnessCompilation(
            userSource,
            buildHarnessDocument,
            references,
            syntaxTreePath: syntaxTreePath);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: generators.Select(static generator => generator.AsSourceGenerator()),
            parseOptions: parseOptions,
            optionsProvider: optionsProvider,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        ThrowGeneratorExceptions(driver.GetRunResult());

        return new CacheTrackingDriver(driver, compilation, parseOptions, buildHarnessDocument);
    }

    public static IncrementalStepRunReason GetStepReason(GeneratorRunResult result, string stepName)
    {
        if (!result.TrackedSteps.TryGetValue(stepName, out var steps))
        {
            throw new InvalidOperationException(
                $"Tracked step '{stepName}' not found. Available: [{string.Join(", ", result.TrackedSteps.Keys)}]");
        }

        return steps[^1].Outputs[^1].Reason;
    }

    public static string ToSnapshot(GeneratorRunOutput output, SnapshotOptions options)
    {
        IEnumerable<Diagnostic> diagnostics = output.Diagnostics
            .Where(diagnostic => diagnostic.Id.StartsWith(options.DiagnosticPrefix, StringComparison.Ordinal));

        if (options.DeduplicateDiagnostics)
        {
            diagnostics = diagnostics
                .GroupBy(static diagnostic => (diagnostic.Id, diagnostic.GetMessage()))
                .Select(static group => group.First());
        }

        IOrderedEnumerable<Diagnostic> orderedDiagnostics =
            diagnostics.OrderBy(
                static diagnostic => diagnostic.Id,
                options.DiagnosticIdComparer ?? StringComparer.Ordinal);
        if (options.ThenByDiagnosticMessage)
        {
            orderedDiagnostics = orderedDiagnostics.ThenBy(
                static diagnostic => diagnostic.GetMessage(),
                StringComparer.Ordinal);
        }

        ImmutableArray<Diagnostic> formattedDiagnostics = orderedDiagnostics.ToImmutableArray();
        var builder = new StringBuilder();
        builder.AppendLine("Diagnostics:");

        foreach (Diagnostic diagnostic in formattedDiagnostics)
        {
            builder.AppendLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        if (formattedDiagnostics.IsDefaultOrEmpty && options.WriteNoneForEmptyDiagnostics)
        {
            builder.AppendLine("  <none>");
        }

        builder.AppendLine();
        builder.AppendLine("Generated Sources:");
        foreach (GeneratedSource source in output.GeneratedSources)
        {
            builder.AppendLine($"--- {source.HintName} ---");
            builder.AppendLine(source.Source.TrimEnd());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static MetadataReference[] GetMetadataReferences(
        string? excludedAssemblyName = null,
        params string[] requiredAssemblyPaths)
    {
        var trustedPlatformAssemblies =
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => excludedAssemblyName is null
                || !string.Equals(Path.GetFileName(path), excludedAssemblyName, StringComparison.OrdinalIgnoreCase))
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToList<MetadataReference>();

        foreach (string path in requiredAssemblyPaths)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Required test reference not found: {path}");
            }

            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references.ToArray();
    }

    static void ThrowGeneratorExceptions(GeneratorDriverRunResult runResult)
    {
        foreach (GeneratorRunResult result in runResult.Results)
        {
            if (result.Exception is not null)
            {
                throw result.Exception;
            }
        }
    }
}

public sealed record GeneratorRunOutput(
    ImmutableArray<GeneratedSource> GeneratedSources,
    ImmutableArray<Diagnostic> Diagnostics);

public sealed record GeneratedSource(string HintName, string Source);

public sealed record SnapshotOptions(
    string DiagnosticPrefix,
    bool DeduplicateDiagnostics = true,
    bool ThenByDiagnosticMessage = false,
    bool WriteNoneForEmptyDiagnostics = true,
    IComparer<string>? DiagnosticIdComparer = null);

public sealed class CacheTrackingDriver
{
    readonly Func<string, string> _buildHarnessDocument;

    public CacheTrackingDriver(
        GeneratorDriver driver,
        CSharpCompilation compilation,
        CSharpParseOptions parseOptions,
        Func<string, string> buildHarnessDocument)
    {
        Driver = driver;
        Compilation = compilation;
        ParseOptions = parseOptions;
        _buildHarnessDocument = buildHarnessDocument;
    }

    public GeneratorDriver Driver { get; }

    public CSharpCompilation Compilation { get; }

    public CSharpParseOptions ParseOptions { get; }

    public GeneratorRunResult RunSecond(CSharpCompilation? editedCompilation = null)
    {
        GeneratorDriver driver = Driver.RunGenerators(editedCompilation ?? Compilation);
        return driver.GetRunResult().Results[0];
    }

    public CSharpCompilation WithUnrelatedTree(string source = "class Dummy {}")
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        return Compilation.AddSyntaxTrees(tree);
    }

    public CSharpCompilation WithAdditionalSource(string userSource)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(_buildHarnessDocument(userSource), ParseOptions);
        return Compilation.AddSyntaxTrees(tree);
    }
}

public sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    readonly AnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
    {
        _globalOptions = new TestAnalyzerConfigOptions(values);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestAnalyzerConfigOptions.Empty;
}

sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    public static readonly TestAnalyzerConfigOptions Empty = new(new Dictionary<string, string>());

    readonly IReadOnlyDictionary<string, string> _values;

    public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
}
