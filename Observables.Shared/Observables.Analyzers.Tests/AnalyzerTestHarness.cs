using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    internal static ImmutableArray<Diagnostic> RunAnalyzers(
        string userSource,
        params DiagnosticAnalyzer[] analyzers) =>
        RunAnalyzers(userSource, additionalReferences: [], analyzers);

    internal static ImmutableArray<Diagnostic> RunAnalyzers(
        string userSource,
        IEnumerable<MetadataReference> additionalReferences,
        params DiagnosticAnalyzer[] analyzers) =>
        RunAnalyzers(userSource, additionalReferences, globalOptions: null, analyzers);

    internal static ImmutableArray<Diagnostic> RunAnalyzers(
        string userSource,
        IEnumerable<MetadataReference> additionalReferences,
        IReadOnlyDictionary<string, string>? globalOptions,
        params DiagnosticAnalyzer[] analyzers)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(userSource, parseOptions);
        var references = GetPlatformReferencesExcludingObservables()
            .Concat(additionalReferences)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var options = globalOptions is null
            ? null
            : new AnalyzerOptions(
                ImmutableArray<AdditionalText>.Empty,
                new DictionaryAnalyzerConfigOptionsProvider(globalOptions));

        var compilationWithAnalyzers = options is null
            ? compilation.WithAnalyzers(analyzers.ToImmutableArray())
            : compilation.WithAnalyzers(analyzers.ToImmutableArray(), options);
        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    internal static IEnumerable<Diagnostic> FilterObservablesDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Where(static d => d.Id.StartsWith("OBS", StringComparison.Ordinal));

    internal static MetadataReference[] GetPlatformReferencesExcludingObservables()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(static path => !Path.GetFileNameWithoutExtension(path).StartsWith("Observables.", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    internal static MetadataReference[] GetMinimalCoreReferences() =>
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
    ];

    internal static MetadataReference CreateReference<T>() =>
        MetadataReference.CreateFromFile(typeof(T).Assembly.Location);

    internal static MetadataReference CreateReferenceFromAssemblyOf(Type type) =>
        MetadataReference.CreateFromFile(type.Assembly.Location);

    sealed class DictionaryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        readonly DictionaryAnalyzerConfigOptions _global;

        internal DictionaryAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values) =>
            _global = new DictionaryAnalyzerConfigOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyAnalyzerConfigOptions.Instance;
    }

    sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        readonly IReadOnlyDictionary<string, string> _values;

        internal DictionaryAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value) =>
            _values.TryGetValue(key, out value!);
    }

    sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        internal static readonly EmptyAnalyzerConfigOptions Instance = new();

        public override bool TryGetValue(string key, out string value)
        {
            value = null!;
            return false;
        }
    }
}
