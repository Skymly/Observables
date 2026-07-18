using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Observables.TestSupport;

public sealed class GeneratorHarnessDefinition
{
    public GeneratorHarnessDefinition(
        Func<string, string> buildHarnessDocument,
        Func<GeneratorHarnessRunOptions, IEnumerable<MetadataReference>> getMetadataReferences,
        Func<IIncrementalGenerator[]> createGenerators,
        SnapshotOptions snapshotOptions,
        bool includeResultDiagnostics = false,
        string? syntaxTreePath = null,
        Func<GeneratorHarnessRunOptions, AnalyzerConfigOptionsProvider?>? createOptionsProvider = null)
    {
        BuildHarnessDocument = buildHarnessDocument;
        GetMetadataReferences = getMetadataReferences;
        CreateGenerators = createGenerators;
        SnapshotOptions = snapshotOptions;
        IncludeResultDiagnostics = includeResultDiagnostics;
        SyntaxTreePath = syntaxTreePath;
        CreateOptionsProvider = createOptionsProvider;
    }

    public Func<string, string> BuildHarnessDocument { get; }

    public Func<GeneratorHarnessRunOptions, IEnumerable<MetadataReference>> GetMetadataReferences { get; }

    public Func<IIncrementalGenerator[]> CreateGenerators { get; }

    public SnapshotOptions SnapshotOptions { get; }

    public bool IncludeResultDiagnostics { get; }

    public string? SyntaxTreePath { get; }

    public Func<GeneratorHarnessRunOptions, AnalyzerConfigOptionsProvider?>? CreateOptionsProvider { get; }
}

public sealed class GeneratorHarnessRunOptions
{
    public LanguageVersion LanguageVersion { get; init; } = LanguageVersion.Preview;

    public bool IncludeCoreReference { get; init; } = true;

    public IIncrementalGenerator[]? Generators { get; init; }

    public IEnumerable<MetadataReference>? ExtraReferences { get; init; }

    public AnalyzerConfigOptionsProvider? OptionsProvider { get; init; }

    public bool? IncludeResultDiagnostics { get; init; }

    public string? SyntaxTreePath { get; init; }
}

public sealed class GeneratorHarness
{
    readonly GeneratorHarnessDefinition _definition;

    public GeneratorHarness(GeneratorHarnessDefinition definition)
    {
        _definition = definition;
    }

    public GeneratorRunOutput Run(
        string userSource,
        GeneratorHarnessRunOptions? options = null)
    {
        options ??= new GeneratorHarnessRunOptions();

        return GeneratorTestRunner.Run(
            userSource,
            _definition.BuildHarnessDocument,
            BuildMetadataReferences(options),
            options.Generators ?? _definition.CreateGenerators(),
            options.LanguageVersion,
            options.OptionsProvider ?? _definition.CreateOptionsProvider?.Invoke(options),
            options.SyntaxTreePath ?? _definition.SyntaxTreePath,
            options.IncludeResultDiagnostics ?? _definition.IncludeResultDiagnostics);
    }

    public CacheTrackingDriver RunWithCacheTracking(
        string userSource,
        GeneratorHarnessRunOptions? options = null)
    {
        options ??= new GeneratorHarnessRunOptions();

        return GeneratorTestRunner.RunWithCacheTracking(
            userSource,
            _definition.BuildHarnessDocument,
            BuildMetadataReferences(options),
            options.Generators ?? _definition.CreateGenerators(),
            options.SyntaxTreePath ?? _definition.SyntaxTreePath,
            options.OptionsProvider ?? _definition.CreateOptionsProvider?.Invoke(options));
    }

    public (CSharpCompilation Compilation, SyntaxTree Tree) CreateHarnessCompilation(
        string userSource,
        GeneratorHarnessRunOptions? options = null)
    {
        options ??= new GeneratorHarnessRunOptions();

        return GeneratorTestRunner.CreateHarnessCompilation(
            userSource,
            _definition.BuildHarnessDocument,
            BuildMetadataReferences(options),
            options.LanguageVersion,
            options.SyntaxTreePath ?? _definition.SyntaxTreePath);
    }

    public IncrementalStepRunReason GetStepReason(
        GeneratorRunResult result,
        string stepName) =>
        GeneratorTestRunner.GetStepReason(result, stepName);

    public string ToSnapshot(GeneratorRunOutput output) =>
        GeneratorTestRunner.ToSnapshot(output, _definition.SnapshotOptions);

    IEnumerable<MetadataReference> BuildMetadataReferences(GeneratorHarnessRunOptions options)
    {
        IEnumerable<MetadataReference> references = _definition.GetMetadataReferences(options);
        if (options.ExtraReferences is not null)
        {
            references = references.Concat(options.ExtraReferences);
        }

        return references;
    }
}

public static class HarnessDocumentBuilder
{
    public static Func<string, string> Create(params string[] namespaces) =>
        userSource =>
        {
            var builder = new System.Text.StringBuilder("#nullable enable\n");
            foreach (string @namespace in namespaces)
            {
                builder.Append("using ").Append(@namespace).AppendLine(";");
            }

            builder.AppendLine();
            builder.Append(userSource);
            builder.AppendLine();
            return builder.ToString();
        };
}

public static class MetadataReferenceBuilder
{
    public static MetadataReference[] Build(params Type?[] assemblyAnchorTypes) =>
        Build(null, assemblyAnchorTypes);

    public static MetadataReference[] Build(
        string? excludedAssemblyName,
        params Type?[] assemblyAnchorTypes) =>
        GeneratorTestRunner.GetMetadataReferences(
            excludedAssemblyName,
            assemblyAnchorTypes
                .Where(static type => type is not null)
                .Select(static type => type!.Assembly.Location)
                .ToArray());
}

public static class SnapshotOptionsFactory
{
    public static SnapshotOptions ForDomain(
        string diagnosticPrefix,
        bool thenByDiagnosticMessage = false) =>
        new(diagnosticPrefix, ThenByDiagnosticMessage: thenByDiagnosticMessage);
}
